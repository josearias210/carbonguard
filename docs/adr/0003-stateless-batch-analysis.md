# ADR 0003: análisis por lote sin persistencia implícita

## Estado

Aceptado.

## Contexto

El ejercicio entrega un conjunto completo de observaciones y solicita clasificarlas. Mantener historial entre requests parece conveniente, pero cambia la semántica del servicio: el resultado pasaría a depender de llamadas anteriores y dejaría de ser reproducible únicamente a partir del request y la versión de política.

Una persistencia correcta también exigiría decisiones que el enunciado no aporta: tenant, identidad estable de sede, correcciones y backfills, idempotencia, orden de llegada, concurrencia, retención, migraciones y reconstrucción de baselines.

## Decisión

- Mantener `POST /analyze` como operación stateless y determinista por lote.
- Construir historiales cronológicos en memoria únicamente durante la petición.
- Limitar cada historial mediante ventanas configurables.
- Incluir la versión de política y la evidencia en la respuesta para reproducibilidad.
- No añadir SQLite, MongoDB ni otra base de datos solo para conservar estado oculto entre ejecuciones.

## Evolución para producción

Separar ingesta y análisis:

1. Persistir observaciones inmutables e identificadas por tenant, sede y período.
2. Publicar trabajos idempotentes particionados por tenant y sede.
3. Mantener snapshots versionados del baseline, reconstruibles desde las observaciones.
4. Aplicar control de concurrencia al actualizar cada partición.
5. Permitir backfills cuando se corrige un dato o cambia la política.
6. Exponer el análisis masivo como job asíncrono con resultados paginados.

SQLite sería apropiado para una demo local de esa arquitectura, pero no resolvería por sí solo las decisiones anteriores y añadirlo al endpoint actual ocultaría su semántica stateful.

## Consecuencias

- El mismo request bajo la misma política produce el mismo resultado.
- No existe dependencia silenciosa del orden de llamadas.
- Cada lote debe incluir el historial necesario para el análisis.
- La persistencia futura requiere un caso de uso explícito de ingesta, no una mutación accidental dentro de `analyze`.
