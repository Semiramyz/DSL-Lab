# CreditRules DSL en C#

Implementación del taller **Diseño de un DSL para reglas de crédito**. El programa representa las reglas como un árbol de sintaxis abstracta (AST), las evalúa sobre un contexto de variables y permite ingresar los datos desde la consola.

## Ejecución

Requiere .NET 10 SDK.

```powershell
dotnet run --project DSL-LAB/DSL-LAB.csproj --urls http://localhost:5091
```

Después abre `http://localhost:5091`. La interfaz ofrece evaluación interactiva, los cinco casos de prueba exigidos, representación gráfica de todos los AST mediante nodos y conectores, y descarga de cada AST como archivo `.dot` compatible con Graphviz. Los casos obligatorios se pueden seleccionar desde el inspector: sus datos se cargan, se evalúan y el inspector enfoca la DSL de `creditoAprobado`.

Las reglas del dominio son seleccionables individualmente. Solo las reglas marcadas se aplican al presionar “Evaluar solicitud” y el sistema permite seleccionar como máximo tres simultáneamente. El contador `0 / 3 aplicadas` muestra el estado actual y la respuesta solo presenta los resultados de esas reglas.

Para los ingresos, cuota inicial y monto solicitado se aceptan valores como `5000000` o `5.000.000`.

## Componentes del AST

| Componente | Función | Equivalencia en esta solución C# |
|---|---|---|
| `ASTNode` | Contrato común de los nodos y operación de evaluación. | `IAstNode`, con `Evaluate`, `ToAscii` y `ToDot`. |
| `VariableNode` | Lee una variable del contexto. | `VariableNode`, por ejemplo `edad`. |
| `GreaterThanOrEqualNode` | Compara dos expresiones usando `>=`. | Clase binaria que devuelve `bool`. |
| `IfStatementNode` | Evalúa una condición y ejecuta la asignación si es verdadera. | `IfStatementNode`. |
| `Contexto` | Ambiente con los valores de entrada y resultados. | `Context`, un diccionario indexado por nombre. |
| Literales | Representan números, textos y booleanos. | `LiteralNode`. |
| Conjunción | Une condiciones con `Y`. | `AndNode`. |

Los nodos binarios adicionales (`LessThanOrEqualNode`, `EqualNode` y `MultiplyNode`) permiten expresar las reglas nuevas sin abandonar el modelo AST.

## Reglas del DSL

Sintaxis conceptual:

```text
SI condicion ENTONCES variable = valor
condicion ::= expresion >= numero | expresion <= numero | expresion == numero
						 | condicion Y condicion
```

Reglas solicitadas:

```text
SI edad >= 18 ENTONCES clienteHabilitado = true
SI ingresos >= 3000000 ENTONCES nivelIngresos = "ALTO"
SI puntaje >= 700 ENTONCES riesgo = "BAJO"
SI edad >= 18 Y ingresos >= 3000000 Y puntaje >= 700
	ENTONCES creditoAprobado = true
```

Tres reglas adicionales implementadas:

```text
SI antiguedadLaboral >= 12 ENTONCES estabilidad = "ALTA"
SI cuotaInicial <= montoSolicitado * 0.35 ENTONCES capacidadPago = "SUFICIENTE"
SI morasHistoricas == 0 ENTONCES historial = "LIMPIO"
```

Cuando una condición no se cumple se conserva un resultado explícito: `false`, `BAJO`, `ALTO`, `BAJA`, `INSUFICIENTE` o `CON MORA`, según la regla.

## Casos de prueba

Los cinco casos del enunciado se ejecutan desde la opción 2:

| Caso | Edad | Ingresos | Puntaje | `creditoAprobado` |
|---:|---:|---:|---:|---|
| 1 | 25 | 5.000.000 | 750 | `true` |
| 2 | 17 | 5.000.000 | 750 | `false` |
| 3 | 25 | 2.000.000 | 750 | `false` |
| 4 | 25 | 5.000.000 | 650 | `false` |
| 5 | 25 | 5.000.000 | 850 | `true` |

Pruebas propuestas para las reglas adicionales:

| Regla | Entrada que cumple | Resultado | Entrada que no cumple | Resultado |
|---|---|---|---|---|
| Estabilidad | antigüedad = 24 meses | `ALTA` | antigüedad = 6 meses | `BAJA` |
| Capacidad | cuota = 1.000.000, monto = 5.000.000 | `SUFICIENTE` | cuota = 2.000.000, monto = 5.000.000 | `INSUFICIENTE` |
| Historial | moras = 0 | `LIMPIO` | moras = 2 | `CON MORA` |

## Diseño conceptual

- **Nombre:** CreditRules DSL.
- **Dominio:** evaluación y clasificación de solicitudes de crédito.
- **Vocabulario:** `SI`, `ENTONCES`, `Y`, `>=`, `<=`, `==`, `edad`, `ingresos`, `puntaje`, `creditoAprobado`, `riesgo` y variables de las reglas adicionales.
- **Operaciones:** lectura de variables, literales, comparaciones, multiplicación, conjunción, asignación y evaluación condicional.
- **Representación:** cada regla se construye mediante nodos AST; no se evalúa concatenando expresiones sin estructura.
- **Edición:** el constructor permite crear o modificar reglas con comparaciones `>=`, `<=` e `==`, y regenera su AST inmediatamente.

## ¿DSL interno o externo?

La solución implementada es un **DSL interno** porque las reglas se construyen usando clases y objetos de C# (`VariableNode`, `AndNode`, `IfStatementNode`, etc.) dentro del programa anfitrión. Aunque se muestra una sintaxis conceptual parecida a lenguaje natural y se exporta el árbol a Graphviz, todavía no existe un lexer/parser que lea reglas escritas desde un archivo independiente. Para convertirlo en DSL externo habría que añadir ese parser y cargar las reglas desde texto.

## Evidencia gráfica del AST

La opción 3 imprime cada árbol en consola con conectores. La opción 4 genera `ast_clienteHabilitado.dot`, `ast_nivelIngresos.dot`, `ast_riesgo.dot`, `ast_creditoAprobado.dot` y los tres archivos de reglas adicionales. Para producir una imagen PNG con Graphviz:

```powershell
dot -Tpng ast_creditoAprobado.dot -o ast_creditoAprobado.png
```

## Bibliografía IEEE

[1] A. V. Aho, M. S. Lam, R. Sethi y J. D. Ullman, *Compilers: Principles, Techniques, and Tools*, 2nd ed. Boston, MA, USA: Pearson, 2006.

[2] Microsoft, “C# language reference,” Microsoft Learn, 2026. [En línea]. Disponible: https://learn.microsoft.com/dotnet/csharp/language-reference/

[3] J. E. Mathewson, “DOT language,” Graphviz Documentation, 2026. [En línea]. Disponible: https://graphviz.org/doc/info/lang.html

## Declaración de herramienta de IA

Para apoyar la organización y revisión del código se utilizó **GitHub Copilot**. Prompt utilizado: “Realiza este taller en C# según lo que pide en la actividad a realizar”, acompañado del enunciado del taller. La evidencia parcial corresponde a la conversación de asistencia y a la salida de la opción de pruebas, donde los cinco casos obligatorios aparecen como `OK`. La revisión, ejecución y explicación final son responsabilidad del grupo.
