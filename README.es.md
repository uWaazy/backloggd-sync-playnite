# Backloggd Sync (Beta)

Plugin de Playnite que envia tus sesiones de juego, estado, nota y plataforma a tu diario de [Backloggd](https://backloggd.com).

> **No oficial.** Sin relacion con Backloggd ni aprobado por el. Backloggd no tiene API publica, asi que el plugin funciona a traves del propio sitio (una ventana de navegador de Playnite con tu inicio de sesion). Nunca ve ni guarda tu contrasena. Para mantener la sesión entre reinicios guarda las cookies de sesión del sitio en la carpeta de datos del plugin (`session-cookies.json`); quien tenga ese archivo podría usar tu sesión, así que no lo compartas (bórralo para olvidar la sesión). Si el sitio muestra una verificacion anti-robot o limita las solicitudes, el envio se pausa y se te avisa.

[English](README.md) | [Portugues](README.pt-BR.md)

## Que hace

- **Tiempo por dia:** al cerrar un juego, la sesion se suma a ese dia dentro de un Log llamado **Playnite** en el diario del juego. Tus propios Logs nunca se modifican.
- **Estado:** el estado de finalizacion de Playnite pasa a Played, Playing o Backlog (reconocido por nombre en portugues, ingles y espanol). Solo se envia cuando cambia en Playnite.
- **Nota:** la puntuacion de usuario de Playnite (0-100) pasa a nota de 1 a 10 (medias estrellas). Solo se envia cuando cambia.
- **Plataforma:** los juegos de PC (Steam, Epic, Game Pass...) pasan a *Windows PC*; las consolas usan su propia plataforma. Solo se rellena cuando el Log aun no tiene una, asi se respeta tu eleccion.
- **Seguro por diseno:** cada envio graba un valor exacto para el dia y luego lee el sitio para confirmarlo. Los fallos quedan en cola y se reintentan; enviar lo mismo dos veces no suma el tiempo dos veces.
- **Vinculo:** los juegos se reconocen por titulo exacto y ano de lanzamiento. Si no hay certeza, clic derecho en el juego > **Backloggd Sync > Vincular juego de Backloggd...** y elige el correcto; todo lo pendiente de ese juego se envia justo despues.
- Menus y mensajes en portugues, ingles y espanol.

## Instalar

1. Descarga el `.pext` desde [Releases](../../releases) y abrelo (o arrastralo a Playnite).
2. Playnite > **Ajustes > Extensiones > Generic > Backloggd Sync (Beta)**.
3. Pulsa **Iniciar sesion en Backloggd**, inicia sesion en la ventana y cierrala. Debe aparecer *Conectado como <tu nombre>*.

Cada release indica el SHA-256 del `.pext` para que lo compares con el archivo descargado.

## Ajustes

- Enviar automaticamente al cerrar un juego
- Enviar tiempo / estado / nota (cada uno se puede desactivar)
- **Enviar pendientes ahora** y un contador de pendientes en vivo

Por juego (clic derecho > Backloggd Sync): vincular el juego, enviar estado y nota ahora, o no enviar nunca el juego.

## Como reportar problemas

Abre una [issue](../../issues/new/choose) con las versiones del plugin y de Playnite y lo que paso. Adjunta las lineas con `[BLS]` de `%AppData%\Playnite\extensions.log`. Incluyen nombres de juegos y fechas, pero no contrasenas ni cookies; revisalas antes de compartirlas.

## Licencia

MIT. Ver [LICENSE](LICENSE).
