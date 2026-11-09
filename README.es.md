# Backloggd Sync (Beta)

[![Apoyar en Ko-fi](https://img.shields.io/badge/Ko--fi-Apoyar-FF5E5B?style=flat&logo=ko-fi&logoColor=white)](https://ko-fi.com/uWaazy)

Plugin de Playnite que envia tus sesiones de juego, estado, nota y plataforma a tu diario de [Backloggd](https://backloggd.com).

> **Beta.** Esta es la primera versión pública. Funciona en el día a día, pero todavía puede tener asperezas. Por favor, reporta los problemas (consulta [Cómo reportar problemas](#cómo-reportar-problemas)).
>
> **No oficial.** Sin relacion con Backloggd ni aprobado por el.

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

## Apoyo

Si este plugin te resulta útil y quieres apoyar su desarrollo continuo:

[![Apoyar en Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/uWaazy)

## Licencia

MIT. Ver [LICENSE](LICENSE).
