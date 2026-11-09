using System;
using System.Collections.Generic;
using System.Globalization;

namespace BackloggdSync
{
    /// <summary>Textos do plugin. Uso: L.T("chave", args). Ordem de cada linha: pt-BR, en, es.</summary>
    public static class L
    {
        public const string Auto = "auto";

        private const int Pt = 0, En = 1, Es = 2;
        private static int _lang = En;

        public static void Configure(string preference, string playniteLanguage)
        {
            var wanted = (preference ?? Auto).Trim().ToLowerInvariant();
            if (wanted == Auto || wanted.Length == 0)
            {
                wanted = (playniteLanguage ?? "").Trim().ToLowerInvariant();
                if (wanted.Length == 0) wanted = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }

            if (wanted.StartsWith("pt", StringComparison.Ordinal)) _lang = Pt;
            else if (wanted.StartsWith("es", StringComparison.Ordinal)) _lang = Es;
            else _lang = En;
        }

        public static string Pick(string[] row)
        {
            if (row == null || row.Length == 0) return "";
            return row[Math.Min(_lang, row.Length - 1)];
        }

        public static string T(string key, params object[] args)
        {
            string[] row;
            if (!Table.TryGetValue(key, out row)) return key;

            var text = row[_lang];
            if (args == null || args.Length == 0) return text;

            try { return string.Format(CultureInfo.CurrentCulture, text, args); }
            catch (FormatException) { return text; }
        }

        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            ["app.name"] = new[] { "Backloggd Sync", "Backloggd Sync", "Backloggd Sync" },
            ["title"] = new[] { "Backloggd Sync: modo captura", "Backloggd Sync: capture mode", "Backloggd Sync: modo captura" },
            ["intro"] = new[]
            {
                "Ferramenta para mapear como o Backloggd funciona (busca, status, nota, diário e calendário). Siga os passos abaixo, um por vez. Em cada passo abre uma janela do site com uma barra de instruções; faça o que ela pede e feche a janela. Nenhum dado é enviado a ninguém: tudo fica num arquivo no seu computador.",
                "Tool to map how Backloggd works (search, status, rating, journal and calendar). Follow the steps below, one at a time. Each step opens a site window with an instruction bar; do what it says and close the window. Nothing is sent to anyone: everything stays in a file on your computer.",
                "Herramienta para mapear cómo funciona Backloggd (búsqueda, estado, nota, diario y calendario). Sigue los pasos, uno por vez. Cada paso abre una ventana del sitio con una barra de instrucciones; haz lo que pide y cierra la ventana. No se envía nada a nadie: todo queda en un archivo en tu equipo."
            },
            ["privacy"] = new[]
            {
                "Senhas, tokens e cookies são mascarados (***). O arquivo gerado contém o seu usuário, nomes de jogos e datas: confira antes de enviar. Use um jogo de teste e remova o que criar no final (há um passo para isso).",
                "Passwords, tokens and cookies are masked (***). The generated file contains your username, game names and dates: review it before sharing. Use a test game and remove what you create at the end (there is a step for that).",
                "Las contraseñas, tokens y cookies se enmascaran (***). El archivo generado contiene tu usuario, nombres de juegos y fechas: revísalo antes de enviarlo. Usa un juego de prueba y borra lo que crees al final (hay un paso para eso)."
            },
            ["progress"] = new[] { "Progresso: {0} de {1} passos concluídos", "Progress: {0} of {1} steps done", "Progreso: {0} de {1} pasos completados" },
            ["step.label"] = new[] { "Passo {0}", "Step {0}", "Paso {0}" },
            ["step.start"] = new[] { "Iniciar passo", "Start step", "Iniciar paso" },
            ["step.redo"] = new[] { "Refazer", "Redo", "Rehacer" },
            ["step.done"] = new[] { "Concluído ({0} requisições, {1})", "Done ({0} requests, {1})", "Completado ({0} solicitudes, {1})" },
            ["step.pending"] = new[] { "Pendente", "Pending", "Pendiente" },
            ["step.empty"] = new[] { "Nenhuma requisição foi registrada neste passo. Refaça e confirme que fez a ação na janela.", "No requests were recorded in this step. Redo it and make sure you did the action in the window.", "No se registró ninguna solicitud en este paso. Rehazlo y confirma que hiciste la acción en la ventana." },
            ["banner.title"] = new[] { "Passo {0}/{1}: {2}", "Step {0}/{1}: {2}", "Paso {0}/{1}: {2}" },
            ["banner.hint"] = new[] { "Quando terminar, feche esta janela. (clique nesta barra para recolher)", "When finished, close this window. (click this bar to collapse)", "Al terminar, cierra esta ventana. (haz clic en esta barra para ocultarla)" },
            ["btn.export"] = new[] { "Gerar arquivo para enviar", "Create file to send", "Crear archivo para enviar" },
            ["btn.folder"] = new[] { "Abrir pasta das capturas", "Open captures folder", "Abrir carpeta de capturas" },
            ["btn.clear"] = new[] { "Apagar capturas", "Delete captures", "Borrar capturas" },
            ["clear.confirm"] = new[] { "Apagar todas as capturas guardadas neste computador?", "Delete all captures saved on this computer?", "¿Borrar todas las capturas guardadas en este equipo?" },
            ["export.none"] = new[] { "Faça pelo menos um passo antes de gerar o arquivo.", "Do at least one step before creating the file.", "Haz al menos un paso antes de crear el archivo." },
            ["export.ok"] = new[] { "Arquivo gerado:\n{0}\n\nEnvie esse arquivo (ele abre numa pasta). Confira antes: ele contém seu usuário, jogos e datas.", "File created:\n{0}\n\nSend this file (its folder opens). Review it first: it contains your username, games and dates.", "Archivo creado:\n{0}\n\nEnvía este archivo (se abre su carpeta). Revísalo antes: contiene tu usuario, juegos y fechas." },
            ["export.err"] = new[] { "Não foi possível gerar o arquivo: {0}", "Could not create the file: {0}", "No se pudo crear el archivo: {0}" },
            ["sync.intro"] = new[]
            {
                "Envia o tempo jogado (por dia), o status e a nota dos seus jogos do Playnite para o seu diário no Backloggd. Não é oficial e não tem ligação com o Backloggd. Você entra no site pela janela do próprio Backloggd; o plugin nunca vê nem guarda a sua senha.",
                "Sends your Playnite playtime (per day), status and rating to your Backloggd journal. Unofficial and not affiliated with Backloggd. You sign in through Backloggd's own window; the plugin never sees or stores your password.",
                "Envía tu tiempo jugado (por día), estado y nota de Playnite a tu diario de Backloggd. No es oficial y no está afiliado a Backloggd. Inicias sesión en la propia ventana de Backloggd; el plugin nunca ve ni guarda tu contraseña."
            },
            ["status.none"] = new[] { "Não conectado ao Backloggd", "Not connected to Backloggd", "No conectado a Backloggd" },
            ["status.user"] = new[] { "Conectado como {0}", "Connected as {0}", "Conectado como {0}" },
            ["status.pending"] = new[] { "{0} envio(s) pendente(s)", "{0} pending item(s)", "{0} envío(s) pendiente(s)" },
            ["status.sending"] = new[] { "enviando...", "sending...", "enviando..." },
            ["status.checking"] = new[] { "Verificando...", "Checking...", "Verificando..." },
            ["btn.login"] = new[] { "Entrar no Backloggd", "Sign in to Backloggd", "Iniciar sesión en Backloggd" },
            ["btn.check"] = new[] { "Verificar conexão", "Check connection", "Verificar conexión" },
            ["btn.sendnow"] = new[] { "Enviar pendências agora", "Send pending now", "Enviar pendientes ahora" },
            ["opt.auto"] = new[] { "Enviar automaticamente ao fechar um jogo", "Send automatically when a game closes", "Enviar automáticamente al cerrar un juego" },
            ["opt.time"] = new[] { "Enviar o tempo jogado (uma sessão por dia no diário)", "Send playtime (one session per day in the journal)", "Enviar el tiempo jugado (una sesión por día en el diario)" },
            ["opt.status"] = new[] { "Enviar o status (Jogado, Jogando, Backlog)", "Send status (Played, Playing, Backlog)", "Enviar el estado (Jugado, Jugando, Backlog)" },
            ["opt.rating"] = new[] { "Enviar a nota (Avaliação do usuário do Playnite)", "Send rating (Playnite user score)", "Enviar la nota (puntuación de usuario de Playnite)" },
            ["opt.note"] = new[]
            {
                "O tempo vai para um Log chamado \"Playnite\" no diário do jogo; seus outros Logs nunca são alterados. Cada envio é conferido relendo o site e, se algo falhar, fica na fila para nova tentativa.",
                "Time goes to a Log named \"Playnite\" in the game's journal; your other Logs are never changed. Each send is verified by reading the site back, and failures stay queued for retry.",
                "El tiempo va a un Log llamado \"Playnite\" en el diario del juego; tus otros Logs nunca se modifican. Cada envío se verifica releyendo el sitio y, si algo falla, queda en cola para reintentar."
            },
            ["capture.header"] = new[] { "Modo captura (avançado)", "Capture mode (advanced)", "Modo captura (avanzado)" },
            ["notify.login"] = new[] { "Backloggd Sync: entre no Backloggd nas configurações do plugin para enviar as pendências.", "Backloggd Sync: sign in to Backloggd in the plugin settings to send pending items.", "Backloggd Sync: inicia sesión en Backloggd en los ajustes del plugin para enviar los pendientes." },
            ["notify.blocked"] = new[] { "Backloggd Sync: o site bloqueou ou limitou as requisições (proteção anti-robô). O envio foi pausado; tente de novo mais tarde.", "Backloggd Sync: the site blocked or rate-limited requests (anti-bot protection). Sending was paused; try again later.", "Backloggd Sync: el sitio bloqueó o limitó las solicitudes (protección anti-robot). El envío se pausó; inténtalo más tarde." },
            ["notify.stuck"] = new[] { "Backloggd Sync: o envio travou e foi cancelado. Tente de novo; se repetir, mande o extensions.log.", "Backloggd Sync: the send got stuck and was cancelled. Try again; if it repeats, send the extensions.log.", "Backloggd Sync: el envío se atascó y se canceló. Inténtalo de nuevo; si se repite, envía el extensions.log." },
            ["notify.unknownpage"] = new[] { "Backloggd Sync: não reconheci a página do Backloggd (você está deslogado ou o site mudou). Clique aqui para entrar de novo; se continuar, o site pode ter mudado.", "Backloggd Sync: I did not recognize the Backloggd page (you are signed out or the site changed). Click here to sign in again; if it keeps happening, the site may have changed.", "Backloggd Sync: no reconocí la página de Backloggd (sesión cerrada o el sitio cambió). Haz clic aquí para entrar de nuevo; si continúa, el sitio puede haber cambiado." },
            ["notify.site"] = new[] { "Backloggd Sync: os envios falharam 3 vezes seguidas mesmo com você conectado. O Backloggd pode ter mudado; veja se há uma versão nova do plugin ou abra uma issue com o extensions.log.", "Backloggd Sync: sends failed 3 times in a row even though you are signed in. Backloggd may have changed; check for a new plugin version or open an issue with the extensions.log.", "Backloggd Sync: los envíos fallaron 3 veces seguidas aunque estás conectado. Backloggd puede haber cambiado; revisa si hay una versión nueva del plugin o abre un issue con el extensions.log." },
            ["pend.header"] = new[] { "Pendências", "Pending items", "Pendientes" },
            ["pend.note"] = new[] { "O que está na fila e por que ainda não foi. Jogos sem vínculo podem ser vinculados aqui mesmo.", "What is in the queue and why it has not gone yet. Unlinked games can be linked right here.", "Lo que está en la cola y por qué aún no salió. Los juegos sin vínculo se pueden vincular aquí mismo." },
            ["pend.none"] = new[] { "Nada pendente.", "Nothing pending.", "Nada pendiente." },
            ["pend.more"] = new[] { "... e mais {0}.", "... and {0} more.", "... y {0} más." },
            ["pend.link"] = new[] { "Vincular...", "Link...", "Vincular..." },
            ["pend.discard"] = new[] { "Descartar", "Discard", "Descartar" },
            ["pend.statusonly"] = new[] { "status e nota", "status and rating", "estado y nota" },
            ["reason.needlink"] = new[] { "Aguardando vínculo: não achei o jogo no Backloggd com certeza.", "Waiting for a link: could not match the game on Backloggd with certainty.", "Esperando vínculo: no pude identificar el juego en Backloggd con certeza." },
            ["reason.error"] = new[] { "Erro na última tentativa: {0}", "Error on the last attempt: {0}", "Error en el último intento: {0}" },
            ["reason.login"] = new[] { "Você não está conectado ao Backloggd (use Entrar no Backloggd).", "You are not signed in to Backloggd (use Sign in to Backloggd).", "No estás conectado a Backloggd (usa Iniciar sesión en Backloggd)." },
            ["reason.blocked"] = new[] { "O site bloqueou ou limitou as requisições; tente mais tarde.", "The site blocked or rate-limited requests; try later.", "El sitio bloqueó o limitó las solicitudes; inténtalo más tarde." },
            ["reason.stuck"] = new[] { "O último envio travou e foi cancelado.", "The last send got stuck and was cancelled.", "El último envío se atascó y se canceló." },
            ["reason.site"] = new[] { "Falhas seguidas: o Backloggd pode ter mudado.", "Repeated failures: Backloggd may have changed.", "Fallos seguidos: Backloggd puede haber cambiado." },
            ["reason.waiting"] = new[] { "Na fila, aguardando o próximo envio.", "Queued, waiting for the next send.", "En cola, esperando el próximo envío." },
            ["link.other"] = new[] { "Nenhum desses: buscar com outro nome...", "None of these: search with another name...", "Ninguno: buscar con otro nombre..." },
            ["link.pick"] = new[] { "Qual versão é '{0}'?", "Which version is '{0}'?", "¿Qué versión es '{0}'?" },
            ["login.ok"] = new[] { "Login feito com sucesso! Conectado como {0}.", "Signed in successfully! Connected as {0}.", "¡Sesión iniciada con éxito! Conectado como {0}." },
            ["login.fail"] = new[] { "Não consegui confirmar o login no Backloggd. Tente de novo (Entrar no Backloggd).", "Could not confirm the Backloggd sign-in. Try again (Sign in to Backloggd).", "No pude confirmar el inicio de sesión en Backloggd. Inténtalo de nuevo (Iniciar sesión en Backloggd)." },
            ["notify.synced"] = new[] { "Backloggd Sync: {0} jogo(s) enviado(s).", "Backloggd Sync: {0} game(s) sent.", "Backloggd Sync: {0} juego(s) enviado(s)." },
            ["notify.needlink"] = new[] { "Backloggd Sync: não achei '{0}' com certeza. Clique com o botão direito no jogo > Backloggd Sync > Vincular jogo.", "Backloggd Sync: could not match '{0}' with certainty. Right-click the game > Backloggd Sync > Link game.", "Backloggd Sync: no pude identificar '{0}' con certeza. Clic derecho en el juego > Backloggd Sync > Vincular juego." },
            ["menu.link"] = new[] { "Vincular jogo do Backloggd...", "Link Backloggd game...", "Vincular juego de Backloggd..." },
            ["menu.syncstatus"] = new[] { "Enviar status e nota agora", "Send status and rating now", "Enviar estado y nota ahora" },
            ["menu.ignore"] = new[] { "Não enviar este jogo", "Do not send this game", "No enviar este juego" },
            ["menu.unignore"] = new[] { "Voltar a enviar este jogo", "Send this game again", "Volver a enviar este juego" },
            ["menu.sendpending"] = new[] { "Enviar pendências agora", "Send pending now", "Enviar pendientes ahora" },
            ["link.title"] = new[] { "Vincular ao Backloggd", "Link to Backloggd", "Vincular a Backloggd" },
            ["link.prompt"] = new[] { "Nome do jogo para buscar no Backloggd:", "Game name to search on Backloggd:", "Nombre del juego a buscar en Backloggd:" },
            ["link.searching"] = new[] { "Buscando no Backloggd...", "Searching Backloggd...", "Buscando en Backloggd..." },
            ["link.none"] = new[] { "Nenhum resultado (ou você não está conectado).", "No results (or you are not signed in).", "Sin resultados (o no has iniciado sesión)." },
            ["link.done"] = new[] { "'{0}' vinculado a '{1}'. Enviando os dados do jogo para o Backloggd...", "'{0}' linked to '{1}'. Sending the game's data to Backloggd...", "'{0}' vinculado a '{1}'. Enviando los datos del juego a Backloggd..." },
            ["window.err"] = new[] { "Não foi possível abrir a janela do site: {0}", "Could not open the site window: {0}", "No se pudo abrir la ventana del sitio: {0}" },
            ["btn.checkupdate"] = new[] { "Verificar atualizações", "Check for updates", "Buscar actualizaciones" },
            ["notify.update"] = new[] { "Nova versão do Backloggd Sync disponível: {0}. Clique para atualizar.", "New Backloggd Sync version available: {0}. Click to update.", "Nueva versión de Backloggd Sync disponible: {0}. Haz clic para actualizar." },
            ["update.confirm"] = new[] { "Deseja baixar e instalar a versão {0} do Backloggd Sync agora?", "Do you want to download and install Backloggd Sync {0} now?", "¿Deseas descargar e instalar Backloggd Sync {0} ahora?" },
            ["update.downloading"] = new[] { "Baixando atualização ({0})...", "Downloading update ({0})...", "Descargando actualización ({0})..." },
            ["update.noupdate"] = new[] { "O Backloggd Sync já está na versão mais recente ({0}).", "Backloggd Sync is already up to date ({0}).", "Backloggd Sync ya está en la versión más reciente ({0})." },
            ["update.failed"] = new[] { "Não foi possível verificar ou baixar a atualização: {0}", "Could not check or download the update: {0}", "No se pudo verificar o descargar la actualización: {0}" }
        };
    }
}
