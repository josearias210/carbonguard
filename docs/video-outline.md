# Guion del video — CarbonGuard (máximo 5 minutos)

Duración objetivo: **4:40–4:50**. Ensayar con cronómetro y no improvisar detalles adicionales.

## Preparación antes de grabar

- Tener abierta la aplicación en Swagger y el repositorio en el editor.
- Preparar los archivos `examples/challenge-request.json` y `examples/business-context-request.json`.
- Aumentar el tamaño de letra y ocultar notificaciones.
- Ejecutar previamente los tests y dejar visible el resultado exitoso.

## 0:00–0:30 — Apertura y resultado

**Pantalla:** Swagger con el endpoint de análisis y la respuesta del dataset original.

**Decir:**

> Hola. Esta es CarbonGuard, una solución en .NET 8 para detectar registros energéticos y de emisiones que deben revisarse antes del reporting ESG. Sobre el dataset proporcionado, la solución identifica los registros 4, 7 y 8, pero por razones distintas: el 4 presenta un cambio energético anormal, el 7 contiene valores imposibles y el 8 tiene una relación sospechosa entre CO₂ y energía. Mi objetivo no fue solamente marcar outliers, sino producir decisiones explicables y auditables.

## 0:30–1:05 — Arquitectura

**Pantalla:** árbol de la solución.

**Decir:**

> Separé la solución en tres proyectos. CarbonGuard.Core contiene el dominio, las estadísticas y las reglas, sin depender de ASP.NET. CarbonGuard.Api expone el caso de uso mediante una Minimal API con validación, Problem Details, health checks y telemetría. CarbonGuard.Tests verifica tanto el comportamiento del motor como el contrato HTTP. Elegí esta estructura porque mantiene aislada la lógica que realmente estamos evaluando, sin introducir persistencia, microservicios u otras piezas que el ejercicio no necesita.

## 1:05–2:20 — Criterio de detección

**Pantalla:** `AnomalyDetector.cs`, después `RobustStatistics.cs` y brevemente `appsettings.json`.

**Decir:**

> El enunciado no proporciona una fórmula, así que hice explícita esa decisión. Primero valido calidad: identificadores, sede, período y valores positivos. Un registro inválido nunca alimenta los baselines.
>
> Para consumo energético comparo cada sede con su propio historial anterior mediante mediana y MAD. Elegí estadística robusta porque la muestra es pequeña y los valores extremos distorsionarían la media y la desviación estándar. También exijo una desviación relativa mínima; ambos umbrales son configurables.
>
> La segunda regla es independiente: calculo la intensidad CO₂ por kilovatio-hora. Esto permite detectar el registro 8 aunque su energía parezca normal. La intensidad prefiere historial de la misma sede y utiliza un fallback global explícito durante cold start.
>
> El procesamiento es cronológico e independiente del orden del JSON. Los outliers no se incorporan al historial posterior, evitando que contaminen el baseline. Cuando no existe evidencia suficiente devuelvo NotEvaluated o PartiallyEvaluated; nunca convierto falta de información en normalidad.

## 2:20–3:05 — Escenario A: contexto de negocio

**Pantalla:** ejecutar `business-context-request.json` y señalar `ExplainedByContext`.

**Decir:**

> En el escenario de ampliación, una anomalía estadística no necesariamente es un error. Modelé la explicación como un BusinessEvent opcional y aprobado, separado de las mediciones originales. El motor verifica sede, período y si el aumento observado cae dentro del rango esperado por negocio.
>
> Si coincide, conserva la detección y su evidencia, pero la clasifica como ExplainedByContext. No la borra. Si la ampliación es estructural, resetsBaseline inicia un nuevo segmento histórico; si es temporal, conserva el anterior. Además, el contexto energético nunca oculta una anomalía independiente de intensidad de CO₂.

## 3:05–3:50 — Escenario B: uso responsable de IA

**Pantalla:** sección “Escenario B: IA” del README o el ADR de IA.

**Decir:**

> No enviaría cada fila a un LLM para preguntarle si es correcta. Las validaciones, divisiones y comparaciones son más precisas, económicas y reproducibles mediante código.
>
> Usaría un LLM después de la detección, para leer correos, facturas o notas operativas y proponer un BusinessEvent estructurado. Le proporcionaría la observación, el baseline, las reglas activadas y documentos recuperados con sus fuentes.
>
> La salida se validaría contra un esquema, incluiría evidencia y quedaría sin aprobar. Una persona autoriza el contexto y el motor determinista vuelve a evaluar. El LLM no tendría permisos para modificar reporting ESG. No integré un proveedor real porque el ejercicio pide explicar el criterio; la API queda preparada para ser utilizada como tool por un agente.

## 3:50–4:25 — Calidad y operación

**Pantalla:** resultado de tests, Dockerfile y respuesta con `policyVersion`.

**Decir:**

> La solución incluye pruebas del dataset original, cold start, independencia del orden, contaminación del baseline y contexto estructural. Se distribuye mediante una imagen multi-stage, ejecuta como usuario no root y expone health checks. Cada respuesta incluye la política aplicada y evidencia por regla, facilitando reproducibilidad y auditoría.

## 4:25–4:45 — Evolución a millones de registros

**Pantalla:** README, sección de escalabilidad.

**Decir:**

> Para millones de registros mantendría el Core, pero cambiaría la ejecución a jobs asíncronos, particionados por sede, con colas, workers idempotentes, almacenamiento de estado y respuestas paginadas. Las ventanas históricas ya están acotadas; añadiría estacionalidad, cohorts comparables y calibración con datos reales etiquetados.

## 4:45–4:55 — Cierre

**Pantalla:** Swagger o README principal.

**Decir:**

> La decisión central fue usar reglas para hechos verificables, contexto aprobado para interpretar cambios de negocio e IA solamente donde aporta comprensión. Así, el resultado permanece explicable, reproducible y auditable.

## Respuestas rápidas si surge una entrevista posterior

### ¿Por qué no utilizaste un agente en el demo?

> Porque no quise usar un LLM como calculadora ni como autoridad del dato. Primero construí el sistema determinista que un agente consumiría como tool. El agente sería una capa de investigación y explicación, no un sustituto del motor.

### ¿Por qué mediana y MAD?

> Porque son robustas frente a los propios outliers que quiero detectar. Con más histórico compararía alternativas y calibraría umbrales según falsos positivos y falsos negativos aceptables para negocio.

### ¿El rango del BusinessEvent viene del enunciado?

> No. Es una decisión de modelado para convertir una explicación cualitativa en evidencia verificable. En producción procedería de planificación u operaciones y requeriría aprobación.

### ¿Por qué una API?

> Porque ofrece un contrato fácil de ejecutar, probar e integrar, incluso como tool de un agente, sin acoplar el motor de dominio al transporte HTTP.
