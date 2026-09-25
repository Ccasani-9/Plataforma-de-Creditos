# Evidencias

Capturas que acompañan las pruebas descritas en el README principal (tomadas el 2026-09-25).

| Archivo | Pregunta | Qué debe mostrar |
|---|---|---|
| `p6-websocket-devtools.png` | 6 | DevTools → Network → WS: conexión `wss://…/hubs/solicitudes` con **101 Switching Protocols** y sus frames |
| `p6-evento-recibido.png` | 6 | Sesión del cliente con el estado actualizado y el aviso, sin recargar, tras la acción del analista |
| `p6-cliente2-no-recibe.png` | 6 | Segundo cliente conectado que **no** recibe el evento |
| `p6-anonimo-401.png` | 6 | Conexión anónima al Hub rechazada (**401**) |
| `p7-cola-pendiente.png` | 7 | CloudAMQP: `solicitudes.notificaciones` con **1 mensaje Ready** (consumidor desactivado) |
| `p7-cola-vacia.png` | 7 | CloudAMQP: cola en **0** tras reactivar el consumidor |
| `p7-mis-notificaciones.png` | 7 | "Mis notificaciones" con **una sola** notificación |
| `p7-reenvio-confirmado.png` | 7 | Reenvío manual del mismo `MessageId` confirmado por el broker |
| `p7-reenvio-sin-duplicado.png` | 7 | Tras el reenvío sigue habiendo **una** notificación (sin duplicado) |
| `p7-log-consumidor.txt` | 7 | Extracto de logs: publicación confirmada, ACK tras guardar y "ya fue procesado" en el reenvío |
