# Política de seguridad

## Versiones soportadas

Este repositorio representa una prueba técnica y solo se mantiene la versión presente en la rama `main`.

## Reportar una vulnerabilidad

No publiques vulnerabilidades, secretos ni datos de clientes en una incidencia pública. Repórtalos mediante el canal privado utilizado durante el proceso de evaluación e incluye:

- Componente y versión afectados.
- Pasos mínimos para reproducir el problema.
- Impacto potencial.
- Mitigación sugerida, si existe.

Evita adjuntar credenciales, información personal o datasets de producción. Los reportes se deben validar antes de convertirlos en una incidencia pública.

## Alcance de seguridad

La API de demostración no implementa autenticación ni autorización. Antes de exponerla fuera de un entorno controlado se requieren identidad, autorización por tenant, TLS en el ingress, gestión de secretos, rate limiting y políticas de retención/auditoría acordes al contexto ESG.

