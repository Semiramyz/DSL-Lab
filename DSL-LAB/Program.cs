using System.Globalization;
using static DSL_LAB.AstHelpers;

namespace DSL_LAB;

public interface IAstNode
{
	object? Evaluate(Context context);
	string ToAscii(string indent = "", bool isLast = true);
	void ToDot(List<string> lines, ref int nextId, int? parentId = null);
}

public sealed class Context
{
	private readonly Dictionary<string, object?> variables = new(StringComparer.OrdinalIgnoreCase);

	public object? this[string name]
	{
		get => variables.TryGetValue(name, out var value) ? value : null;
		set => variables[name] = value;
	}

	public IReadOnlyDictionary<string, object?> Variables => variables;
}

public sealed class LiteralNode(object? value) : IAstNode
{
	public object? Evaluate(Context context) => value;
	public string ToAscii(string indent = "", bool isLast = true) => $"{indent}{(isLast ? "└── " : "├── ")}Literal({Format(value)})\n";
	public void ToDot(List<string> lines, ref int nextId, int? parentId = null) => DotNode(lines, ref nextId, parentId, $"Literal\\n{Format(value).Replace("\"", "\\\"")}");

	private static string Format(object? item) => item switch
	{
		null => "null",
		string text => $"\"{text}\"",
		bool boolean => boolean.ToString().ToLowerInvariant(),
		_ => Convert.ToString(item, CultureInfo.InvariantCulture) ?? "null"
	};
}

public sealed class VariableNode(string name) : IAstNode
{
	public object? Evaluate(Context context) => context[name];
	public string ToAscii(string indent = "", bool isLast = true) => $"{indent}{(isLast ? "└── " : "├── ")}Variable({name})\n";
	public void ToDot(List<string> lines, ref int nextId, int? parentId = null) => DotNode(lines, ref nextId, parentId, $"Variable\\n{name}");
}

public abstract class BinaryNode(string symbol, IAstNode left, IAstNode right) : IAstNode
{
	protected IAstNode Left { get; } = left;
	protected IAstNode Right { get; } = right;
	protected string Symbol { get; } = symbol;
	public abstract object? Evaluate(Context context);
	public string ToAscii(string indent = "", bool isLast = true)
	{
		var result = $"{indent}{(isLast ? "└── " : "├── ")}{GetType().Name.Replace("Node", "")} ({Symbol})\n";
		result += Left.ToAscii(indent + (isLast ? "    " : "│   "), false);
		result += Right.ToAscii(indent + (isLast ? "    " : "│   "), true);
		return result;
	}
	public void ToDot(List<string> lines, ref int nextId, int? parentId = null)
	{
		var id = DotNode(lines, ref nextId, parentId, $"{GetType().Name.Replace("Node", "")}\\n{Symbol}");
		Left.ToDot(lines, ref nextId, id);
		Right.ToDot(lines, ref nextId, id);
	}
}

public sealed class GreaterThanOrEqualNode(IAstNode left, IAstNode right) : BinaryNode(">=", left, right)
{
	public override object Evaluate(Context context) => Number(Left.Evaluate(context)) >= Number(Right.Evaluate(context));
}

public sealed class LessThanOrEqualNode(IAstNode left, IAstNode right) : BinaryNode("<=", left, right)
{
	public override object Evaluate(Context context) => Number(Left.Evaluate(context)) <= Number(Right.Evaluate(context));
}

public sealed class EqualNode(IAstNode left, IAstNode right) : BinaryNode("==", left, right)
{
	public override object Evaluate(Context context)
	{
		var leftValue = Left.Evaluate(context);
		var rightValue = Right.Evaluate(context);
		return leftValue is IConvertible && rightValue is IConvertible
			? Number(leftValue) == Number(rightValue)
			: Equals(leftValue, rightValue);
	}
}

public sealed class AndNode(IAstNode left, IAstNode right) : BinaryNode("Y", left, right)
{
	public override object Evaluate(Context context) => Convert.ToBoolean(Left.Evaluate(context)) && Convert.ToBoolean(Right.Evaluate(context));
}

public sealed class MultiplyNode(IAstNode left, IAstNode right) : BinaryNode("*", left, right)
{
	public override object Evaluate(Context context) => Number(Left.Evaluate(context)) * Number(Right.Evaluate(context));
}

public sealed class AssignmentNode(string name, IAstNode value) : IAstNode
{
	public object? Evaluate(Context context)
	{
		var evaluated = value.Evaluate(context);
		context[name] = evaluated;
		return evaluated;
	}

	public string ToAscii(string indent = "", bool isLast = true)
	{
		var result = $"{indent}{(isLast ? "└── " : "├── ")}Assignment({name})\n";
		return result + value.ToAscii(indent + (isLast ? "    " : "│   "), true);
	}

	public void ToDot(List<string> lines, ref int nextId, int? parentId = null)
	{
		var id = DotNode(lines, ref nextId, parentId, $"Assignment\\n{name}");
		value.ToDot(lines, ref nextId, id);
	}
}

public sealed class IfStatementNode(IAstNode condition, AssignmentNode thenBranch) : IAstNode
{
	public object? Evaluate(Context context)
	{
		if (Convert.ToBoolean(condition.Evaluate(context)))
			thenBranch.Evaluate(context);
		return context;
	}

	public string ToAscii(string indent = "", bool isLast = true)
	{
		var result = $"{indent}{(isLast ? "└── " : "├── ")}IfStatement(SI...ENTONCES)\n";
		result += condition.ToAscii(indent + (isLast ? "    " : "│   "), false);
		return result + thenBranch.ToAscii(indent + (isLast ? "    " : "│   "), true);
	}

	public void ToDot(List<string> lines, ref int nextId, int? parentId = null)
	{
		var id = DotNode(lines, ref nextId, parentId, "IfStatement\\nSI...ENTONCES");
		condition.ToDot(lines, ref nextId, id);
		thenBranch.ToDot(lines, ref nextId, id);
	}
}

public static class AstHelpers
{
	public static double Number(object? value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);

	public static int DotNode(List<string> lines, ref int nextId, int? parentId, string label)
	{
		var id = nextId++;
		lines.Add($"  n{id} [label=\"{label}\"];" );
		if (parentId.HasValue)
			lines.Add($"  n{parentId.Value} -> n{id};");
		return id;
	}
}

public sealed record ApplicationInput(
	double Edad,
	double Ingresos,
	double Puntaje,
	double AntiguedadLaboral,
	double CuotaInicial,
	double MontoSolicitado,
	double MorasHistoricas,
	string[]? SelectedRules = null);

public sealed record RuleInput(
	string Name,
	string ConditionVariable,
	string Operator,
	string ConditionValue,
	string ConditionValueType,
	string ActionVariable,
	string ActionValue,
	string ActionValueType);

public static class Program
{
	private static readonly List<(string Name, IfStatementNode Rule)> Rules = BuildRules();
	private static readonly Dictionary<string, string> CustomSyntax = new(StringComparer.OrdinalIgnoreCase);
	private static readonly string[] InputNames = ["edad", "ingresos", "puntaje", "antiguedadLaboral", "cuotaInicial", "montoSolicitado", "morasHistoricas"];

	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);
		var app = builder.Build();
		app.UseDefaultFiles();
		app.UseStaticFiles();

		app.MapPost("/api/evaluate", (ApplicationInput input) =>
		{
			if (input.SelectedRules is { Length: > 3 }) return Results.BadRequest(new { error = "Solo puedes aplicar un máximo de 3 reglas." });
			var context = CreateContext(input);
			EvaluateAll(context, input.SelectedRules);
			return Results.Ok(new { inputs = InputValues(context), results = ResultValues(context, input.SelectedRules), appliedRules = input.SelectedRules ?? [] });
		});

		app.MapGet("/api/rules", () => Rules.Select(rule => new { id = rule.Name, syntax = Syntax(rule.Name), ast = rule.Rule.ToAscii() }));
		app.MapPost("/api/rules", (RuleInput input) => SaveRule(input, false));
		app.MapPut("/api/rules/{name}", (string name, RuleInput input) => SaveRule(input with { Name = name }, true));
		app.MapGet("/api/ast/{name}", (string name) =>
		{
			var rule = Rules.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			if (rule.Rule is null) return Results.NotFound();
			var lines = new List<string> { "digraph AST {", "  node [shape=box, fontname=Arial];" };
			var nextId = 0;
			rule.Rule.ToDot(lines, ref nextId);
			lines.Add("}");
			return Results.Text(string.Join(Environment.NewLine, lines), "text/vnd.graphviz");
		});

		app.MapGet("/api/tests", () => RequiredTests());
		app.Run();
	}

	private static Context CreateContext(ApplicationInput input) => new()
	{
		["edad"] = input.Edad,
		["ingresos"] = input.Ingresos,
		["puntaje"] = input.Puntaje,
		["antiguedadLaboral"] = input.AntiguedadLaboral,
		["cuotaInicial"] = input.CuotaInicial,
		["montoSolicitado"] = input.MontoSolicitado,
		["morasHistoricas"] = input.MorasHistoricas
	};

	private static void EvaluateAll(Context context, IEnumerable<string>? selectedRules = null)
	{
		context["clienteHabilitado"] = false;
		context["nivelIngresos"] = "BAJO";
		context["riesgo"] = "ALTO";
		context["creditoAprobado"] = false;
		context["estabilidad"] = "BAJA";
		context["capacidadPago"] = "INSUFICIENTE";
		context["historial"] = "CON MORA";
		var selected = selectedRules is null ? Rules.Select(rule => rule.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) : selectedRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var rule in Rules.Where(rule => selected.Contains(rule.Name))) rule.Rule.Evaluate(context);
	}

	private static object InputValues(Context context) => context.Variables.Where(pair => InputNames.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
	private static object ResultValues(Context context) => context.Variables.Where(pair => !InputNames.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
	private static object ResultValues(Context context, IEnumerable<string>? selectedRules)
	{
		if (selectedRules is null) return ResultValues(context);
		var outputNames = selectedRules.Select(name => Syntax(name).Split(" ENTONCES ").LastOrDefault()?.Split(" = ").FirstOrDefault()).Where(name => !string.IsNullOrWhiteSpace(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
		return context.Variables.Where(pair => outputNames.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
	}

	private static IResult SaveRule(RuleInput input, bool editing)
	{
		if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.ConditionVariable) || string.IsNullOrWhiteSpace(input.ActionVariable) || string.IsNullOrWhiteSpace(input.ActionValue))
			return Results.BadRequest(new { error = "Completa el nombre, la condición y la acción." });
		var rule = CreateRule(input);
		var index = Rules.FindIndex(item => item.Name.Equals(input.Name, StringComparison.OrdinalIgnoreCase));
		if (editing && index < 0) return Results.NotFound();
		if (!editing && index >= 0) return Results.Conflict(new { error = "Ya existe una regla con ese nombre." });
		if (editing) Rules[index] = (input.Name, rule); else Rules.Add((input.Name, rule));
		CustomSyntax[input.Name] = RuleSyntax(input);
		return Results.Ok(new { id = input.Name, syntax = Syntax(input.Name), ast = rule.ToAscii() });
	}

	private static IfStatementNode CreateRule(RuleInput input)
	{
		IAstNode left = new VariableNode(input.ConditionVariable);
		IAstNode right = input.ConditionValueType == "number" ? new LiteralNode(double.Parse(input.ConditionValue, CultureInfo.InvariantCulture)) : new LiteralNode(input.ConditionValue);
		IAstNode condition = input.Operator switch { ">=" => new GreaterThanOrEqualNode(left, right), "<=" => new LessThanOrEqualNode(left, right), "==" => new EqualNode(left, right), _ => throw new ArgumentException("Operador no soportado") };
		object value = input.ActionValueType == "boolean" ? bool.Parse(input.ActionValue) : input.ActionValueType == "number" ? double.Parse(input.ActionValue, CultureInfo.InvariantCulture) : input.ActionValue;
		return new IfStatementNode(condition, new AssignmentNode(input.ActionVariable, new LiteralNode(value)));
	}

	private static object RequiredTests()
	{
		var cases = new[] { (25d, 5000000d, 750d, true), (17d, 5000000d, 750d, false), (25d, 2000000d, 750d, false), (25d, 5000000d, 650d, false), (25d, 5000000d, 850d, true) };
		return cases.Select((test, index) =>
		{
			var context = new Context { ["edad"] = test.Item1, ["ingresos"] = test.Item2, ["puntaje"] = test.Item3, ["antiguedadLaboral"] = 24d, ["cuotaInicial"] = 1000000d, ["montoSolicitado"] = 5000000d, ["morasHistoricas"] = 0d };
			EvaluateAll(context);
			var actual = Convert.ToBoolean(context["creditoAprobado"]);
			return new { id = index + 1, edad = test.Item1, ingresos = test.Item2, puntaje = test.Item3, expected = test.Item4, actual, passed = actual == test.Item4 };
		});
	}

	private static string Syntax(string name) => name switch
	{
		"clienteHabilitado" => "SI edad >= 18 ENTONCES clienteHabilitado = true",
		"nivelIngresos" => "SI ingresos >= 3.000.000 ENTONCES nivelIngresos = ALTO",
		"riesgo" => "SI puntaje >= 700 ENTONCES riesgo = BAJO",
		"creditoAprobado" => "SI edad >= 18 Y ingresos >= 3.000.000 Y puntaje >= 700 ENTONCES creditoAprobado = true",
		"estabilidad" => "SI antiguedadLaboral >= 12 ENTONCES estabilidad = ALTA",
		"capacidadPago" => "SI cuotaInicial <= montoSolicitado * 0.35 ENTONCES capacidadPago = SUFICIENTE",
		_ => CustomSyntax.TryGetValue(name, out var syntax) ? syntax : "SI morasHistoricas == 0 ENTONCES historial = LIMPIO"
	};

	private static string RuleSyntax(RuleInput input) => $"SI {input.ConditionVariable} {input.Operator} {input.ConditionValue} ENTONCES {input.ActionVariable} = {input.ActionValue}";

	private static List<(string Name, IfStatementNode Rule)> BuildRules()
	{
		IAstNode allConditions = new AndNode(new GreaterThanOrEqualNode(new VariableNode("edad"), new LiteralNode(18)), new AndNode(new GreaterThanOrEqualNode(new VariableNode("ingresos"), new LiteralNode(3000000)), new GreaterThanOrEqualNode(new VariableNode("puntaje"), new LiteralNode(700))));
		return
		[
			("clienteHabilitado", new IfStatementNode(new GreaterThanOrEqualNode(new VariableNode("edad"), new LiteralNode(18)), new AssignmentNode("clienteHabilitado", new LiteralNode(true)))),
			("nivelIngresos", new IfStatementNode(new GreaterThanOrEqualNode(new VariableNode("ingresos"), new LiteralNode(3000000)), new AssignmentNode("nivelIngresos", new LiteralNode("ALTO")))),
			("riesgo", new IfStatementNode(new GreaterThanOrEqualNode(new VariableNode("puntaje"), new LiteralNode(700)), new AssignmentNode("riesgo", new LiteralNode("BAJO")))),
			("creditoAprobado", new IfStatementNode(allConditions, new AssignmentNode("creditoAprobado", new LiteralNode(true)))),
			("estabilidad", new IfStatementNode(new GreaterThanOrEqualNode(new VariableNode("antiguedadLaboral"), new LiteralNode(12)), new AssignmentNode("estabilidad", new LiteralNode("ALTA")))),
			("capacidadPago", new IfStatementNode(new LessThanOrEqualNode(new VariableNode("cuotaInicial"), new MultiplyNode(new VariableNode("montoSolicitado"), new LiteralNode(0.35))), new AssignmentNode("capacidadPago", new LiteralNode("SUFICIENTE")))),
			("historial", new IfStatementNode(new EqualNode(new VariableNode("morasHistoricas"), new LiteralNode(0)), new AssignmentNode("historial", new LiteralNode("LIMPIO"))))
		];
	}
}
