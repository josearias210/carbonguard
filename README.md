# CarbonGuard

API en .NET 8 para detectar consumos energéticos y emisiones de CO₂ sospechosos antes de incorporarlos al reporting ESG. El diseño prioriza decisiones **explicables, reproducibles y auditables**.

## Ejecutar con Docker

Requisito: Docker con Docker Compose.

```bash
docker compose up --build
```

- Swagger UI: <http://localhost:8080/swagger>
- Health check: <http://localhost:8080/health/live>
- Endpoint: `POST /api/v1/anomaly-detection/analyze`

El puerto externo es configurable:

```powershell
$env:CARBONGUARD_PORT=8088
docker compose up --build
```

## Ejecutar localmente

Requisito: .NET SDK 8.0.423 o un parche posterior de .NET 8.

```bash
dotnet restore --locked-mode
dotnet test --configuration Release
dotnet run --project src/CarbonGuard.Api
```

Ejemplo en PowerShell:

```powershell
Invoke-RestMethod `
  -Uri http://localhost:8080/api/v1/anomaly-detection/analyze `
  -Method Post `
  -ContentType application/json `
  -InFile ./examples/challenge-request.json
```

También puedes ejecutar las peticiones incluidas en [`examples/CarbonGuard.http`](examples/CarbonGuard.http) desde Visual Studio, Rider o una extensión REST compatible.

## Resultado del reto

La solución envía a revisión los IDs **4, 7 y 8**:

| ID | Resultado | Razón |
|---:|---|---|
| 4 | `High` | Consumo muy superior al histórico de Madrid. Su intensidad de emisiones sigue siendo coherente. |
| 7 | `Critical` | Consumo y emisiones negativos. |
| 8 | `High` | Consumo normal para Barcelona, pero relación CO₂/energía desproporcionada. |

## Criterio

1. **Data quality:** valida valores, IDs, meses y duplicados por sede/mes. Los registros inválidos no alimentan baselines.
2. **Energy behavior:** compara cada sede solo con sus períodos anteriores mediante mediana y `MAD`:

   ```text
   robustZ = 0.6745 × |observed - median| / MAD
   ```

3. **Emission intensity:** evalúa `co2Kg / energyKwh`. Prefiere el histórico de la sede y usa un baseline global únicamente como fallback explícito durante `cold start`.
4. **Uncertainty:** histórico insuficiente produce `PartiallyEvaluated`/`NotEvaluated`, no un falso “normal”.
5. **No contamination:** una observación marcada no entra en el baseline posterior de esa regla.

Los registros se procesan cronológicamente y por período; todos los elementos de un mes se evalúan antes de actualizar baselines. Así, el resultado no depende del orden del JSON. Las ventanas y umbrales son configurables en `appsettings.json`.

Cada respuesta contiene versión del motor, política aplicada, hallazgos, evidencia y evaluación de cada regla —incluyendo alcance y tamaño del baseline— para poder reproducir la decisión.

## Escenario A: contexto de negocio

La ampliación no convierte el dato en estadísticamente normal, sino en una anomalía **explicada**. Un `BusinessEvent` aprobado puede producir `ENERGY_CHANGE_EXPLAINED_BY_APPROVED_CONTEXT`, manteniendo la evidencia. Una anomalía independiente en intensidad de CO₂ continúa bloqueando el registro.

El evento distingue entre contexto temporal y cambio estructural. `resetsBaseline: true` inicia un nuevo segmento histórico después de una ampliación; los primeros meses del nuevo régimen quedan honestamente en `cold start` hasta reunir evidencia suficiente. Véase `examples/business-context-request.json`.

## Escenario B: IA

Código determinista resuelve validaciones, cálculos, severidad, cuarentena y autorización para reporting. Usaría un LLM para extraer candidatos a eventos desde facturas, correos o partes de mantenimiento, proporcionándole sede, período, unidades, histórico, reglas activadas y fuente documental.

La salida del LLM sería estructurada y validada, sin permisos para modificar reporting. El flujo sería:

```text
registro → reglas → contexto sugerido por LLM → aprobación humana → reglas → reporting ESG
```

## Diseño y operación

```text
CarbonGuard.Api   Minimal API, OpenAPI, Problem Details, health, telemetría
       │
CarbonGuard.Core  dominio, reglas y estadísticas sin dependencia de ASP.NET
       │
CarbonGuard.Tests pruebas unitarias y de integración HTTP
```

- Coste aproximado del motor: `O(n × w log w)`, con ventanas pequeñas y acotadas.
- Límite por request: 10.000 registros y 2 MiB de entrada.
- Para millones: jobs asíncronos, partición tenant/sede, estadísticas persistidas, idempotencia, colas y resultados paginados.
- Docker multi-stage; runtime Alpine de unos 49 MB, usuario `app` no-root, filesystem read-only y health check.
- Dependencias bloqueadas; CI verifica formato, build, pruebas, cobertura e imagen.
- 14 pruebas; última ejecución: 92% de cobertura total y 97% en `CarbonGuard.Core`.
- Sin vulnerabilidades NuGet conocidas en la verificación realizada.

No se añadió una base de datos ni un LLM real porque el reto no los requiere. Hacerlo aumentaría coste y superficie de fallo sin mejorar la decisión evaluada.

`POST /analyze` es deliberadamente stateless: construye historiales acotados dentro del lote y no depende de requests anteriores. Esto mantiene la evaluación reproducible. En producción, la ingesta persistente y los baselines versionados serían un caso de uso separado, con idempotencia, partición por tenant/sede y soporte para backfills.

## Decisiones detalladas

- [ADR 0001: núcleo determinista y LLM consultivo](docs/adr/0001-deterministic-core-and-advisory-llm.md)
- [ADR 0002: baselines jerárquicos y acotados](docs/adr/0002-hierarchical-rolling-baselines.md)
- [ADR 0003: análisis por lote sin persistencia implícita](docs/adr/0003-stateless-batch-analysis.md)
- [Guion sugerido para el video](docs/video-outline.md)
- [Guía de contribución](CONTRIBUTING.md)
- [Política de seguridad](SECURITY.md)
