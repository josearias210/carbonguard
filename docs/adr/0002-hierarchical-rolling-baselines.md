# ADR 0002: baselines jerárquicos, cronológicos y acotados

- Estado: aceptado
- Fecha: 2026-08-12

## Contexto

Las sedes pueden tener factores de emisión distintos. Mezclarlas siempre en un único baseline provoca falsos positivos; exigir histórico local desde el primer día deja sin detectar incoherencias durante el `cold start`. Además, recalcular el histórico completo para cada registro no escala y permitir que un outlier alimente el baseline contamina decisiones posteriores.

## Decisión

- Procesar registros en orden cronológico y agruparlos por período.
- Evaluar todo el período antes de actualizar baselines para eliminar dependencia del orden de entrada.
- Usar ventanas acotadas configurables.
- Preferir baseline de intensidad por sede y usar el global solo como fallback explícito.
- No incorporar al baseline una observación marcada por la regla correspondiente.
- Representar histórico insuficiente como `NotEvaluated`, no como regla superada.
- Permitir que un evento estructural aprobado inicie un nuevo segmento mediante `resetsBaseline`.

## Consecuencias

- Complejidad acotada por el tamaño de ventana, apropiada para lotes grandes.
- Menor contaminación y mejor adaptación a drift.
- Las respuestas son más extensas porque incluyen evaluaciones y alcance del baseline.
- El fallback global es una decisión de baja confianza y debe sustituirse por cohorts compatibles cuando exista metadata suficiente (tipo de sede, fuente energética, país o factor contractual).

