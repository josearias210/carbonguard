# ADR 0001: núcleo determinista y LLM consultivo

- Estado: aceptado
- Fecha: 2026-08-12

## Contexto

El resultado puede afectar reporting ESG. Una clasificación no explicable o no reproducible crea riesgo de auditoría, mientras que las notas de clientes y documentos operativos sí pueden requerir interpretación de lenguaje natural.

## Decisión

Las validaciones, cálculos, umbrales, severidades y decisiones de cuarentena son deterministas y versionables. Un LLM podría extraer candidatos a `BusinessEvent` desde texto no estructurado, pero el evento solo modifica el comportamiento cuando ha sido validado y aprobado.

El LLM no escribe ni aprueba datos en el reporting ESG. Su salida debe cumplir un esquema, conservar evidencia y pasar por reglas y revisión humana.

## Consecuencias

- Resultados reproducibles y auditables.
- Operación disponible aunque el proveedor de IA falle.
- Menor riesgo de alucinaciones con impacto regulatorio.
- Se necesita un flujo explícito para aprobar eventos de negocio.

