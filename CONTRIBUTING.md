# Contribuir a CarbonGuard

## Requisitos

- .NET SDK definido en `global.json`.
- Docker con Docker Compose para verificar el contenedor.

## Preparar el entorno

```bash
dotnet restore CarbonGuard.sln --locked-mode
dotnet build CarbonGuard.sln --configuration Release --no-restore
dotnet test CarbonGuard.sln --configuration Release --no-build
```

## Flujo de cambios

1. Crea una rama descriptiva desde `main`.
2. Mantén las reglas de dominio independientes de ASP.NET Core.
3. Añade o actualiza pruebas para todo cambio de comportamiento.
4. Si una decisión cambia arquitectura, seguridad, reporting ESG o el uso de IA, crea un ADR en `docs/adr`.
5. Ejecuta la verificación completa antes de abrir un pull request.

```bash
dotnet format CarbonGuard.sln --verify-no-changes --no-restore
dotnet test CarbonGuard.sln --configuration Release --collect:"XPlat Code Coverage"
dotnet list CarbonGuard.sln package --vulnerable --include-transitive
docker compose build
```

## Convenciones

- Los nombres técnicos y el código se escriben en inglés.
- Los commits deben ser pequeños, enfocados y explicar el motivo del cambio.
- No incluyas secretos, payloads reales de clientes ni datos personales en código, logs, fixtures o incidencias.
- Un resultado `NotEvaluated` no debe transformarse en `Passed` sin evidencia histórica suficiente.
- Un LLM no puede autorizar directamente la incorporación de datos al reporting ESG.

## Pull requests

Describe el problema, la decisión, sus trade-offs y la evidencia de verificación. No es necesario introducir compatibilidad hacia atrás mientras la API permanezca en `v1`, pero todo cambio de contrato debe quedar explícito.

