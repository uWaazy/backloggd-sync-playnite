using System.Collections.Generic;

namespace BackloggdSync
{
    public class CaptureStep
    {
        public string Id;
        public string StartUrl;
        public string[] Title;
        public string[] Instruction;
    }

    /// <summary>Roteiro de captura: cada passo pede uma ação no site para mapear a rota correspondente.</summary>
    public static class CaptureSteps
    {
        public const string Site = "https://backloggd.com/";

        public static readonly List<CaptureStep> All = new List<CaptureStep>
        {
            new CaptureStep
            {
                Id = "login", StartUrl = Site,
                Title = new[] { "Entrar na conta", "Log in", "Iniciar sesión" },
                Instruction = new[]
                {
                    "Entre na sua conta do Backloggd nesta janela (se já estiver logado, só confirme que o seu nome aparece). Depois feche a janela.",
                    "Log in to your Backloggd account in this window (if you already are, just check that your name shows up). Then close the window.",
                    "Inicia sesión en tu cuenta de Backloggd en esta ventana (si ya lo estás, confirma que aparece tu nombre). Luego cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "search", StartUrl = Site,
                Title = new[] { "Buscar um jogo", "Search for a game", "Buscar un juego" },
                Instruction = new[]
                {
                    "Use a busca do site para procurar \"Hades\": digite devagar, espere a lista de sugestões aparecer e depois abra a página completa de resultados (Enter). Feche a janela.",
                    "Use the site search to look for \"Hades\": type slowly, wait for the suggestion list and then open the full results page (Enter). Close the window.",
                    "Usa la búsqueda del sitio para buscar \"Hades\": escribe despacio, espera la lista de sugerencias y abre la página completa de resultados (Enter). Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "game_page", StartUrl = Site,
                Title = new[] { "Abrir a página de um jogo", "Open a game page", "Abrir la página de un juego" },
                Instruction = new[]
                {
                    "Abra a página do jogo Hades e role até ver os botões de status, nota e diário. Não clique em nada de status ou nota ainda. Feche a janela.",
                    "Open the Hades game page and scroll until you see the status, rating and journal buttons. Do not click any status or rating yet. Close the window.",
                    "Abre la página del juego Hades y baja hasta ver los botones de estado, nota y diario. No hagas clic en estado ni nota todavía. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "status", StartUrl = Site,
                Title = new[] { "Marcar o status do jogo", "Set the game status", "Marcar el estado del juego" },
                Instruction = new[]
                {
                    "Na página do Hades: marque como Playing (jogando); depois troque para Played (jogado); depois Backlog; depois Wishlist; por fim remova o status clicando de novo no botão marcado. Espere 2 segundos entre cada clique. Se algum botão tiver outro nome, use o equivalente. Feche a janela.",
                    "On the Hades page: set Playing; then switch to Played; then Backlog; then Wishlist; finally remove the status by clicking the selected button again. Wait 2 seconds between clicks. If a button has another name, use the equivalent. Close the window.",
                    "En la página de Hades: marca Playing; luego cambia a Played; luego Backlog; luego Wishlist; al final quita el estado haciendo clic otra vez en el botón marcado. Espera 2 segundos entre clics. Si un botón tiene otro nombre, usa el equivalente. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "rating", StartUrl = Site,
                Title = new[] { "Dar uma nota", "Rate the game", "Poner una nota" },
                Instruction = new[]
                {
                    "No Hades: dê 3 estrelas; depois mude para 4,5 estrelas; depois remova a nota. Espere 2 segundos entre cada ação. Feche a janela.",
                    "On Hades: give 3 stars; then change to 4.5 stars; then remove the rating. Wait 2 seconds between actions. Close the window.",
                    "En Hades: pon 3 estrellas; luego cambia a 4,5; luego quita la nota. Espera 2 segundos entre acciones. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "journal_create", StartUrl = Site,
                Title = new[] { "Criar uma entrada no diário", "Create a journal entry", "Crear una entrada en el diario" },
                Instruction = new[]
                {
                    "Abra o diário (log) do Hades e crie uma entrada: data de hoje, 1h30 de tempo jogado, plataforma PC, e marque a opção de \"comecei\" (se existir). Preencha todos os campos que o formulário oferecer e salve. Feche a janela.",
                    "Open the journal (log) of Hades and create an entry: today's date, 1h30 played, platform PC, and tick the \"started\" option (if present). Fill every field the form offers and save. Close the window.",
                    "Abre el diario (log) de Hades y crea una entrada: fecha de hoy, 1h30 jugadas, plataforma PC y marca la opción de \"empecé\" (si existe). Rellena todos los campos del formulario y guarda. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "journal_other_day", StartUrl = Site,
                Title = new[] { "Entrada em outro dia", "Entry on another day", "Entrada en otro día" },
                Instruction = new[]
                {
                    "Crie outra entrada no diário do Hades com a data de ontem, 45 minutos, e marque \"terminei\" (completei) se existir. Salve e feche a janela.",
                    "Create another journal entry for Hades dated yesterday, 45 minutes, and tick \"finished\" (completed) if present. Save and close the window.",
                    "Crea otra entrada en el diario de Hades con fecha de ayer, 45 minutos, y marca \"terminé\" (completado) si existe. Guarda y cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "journal_same_day", StartUrl = Site,
                Title = new[] { "Outra sessão no mesmo dia", "Another session on the same day", "Otra sesión el mismo día" },
                Instruction = new[]
                {
                    "Crie mais uma entrada na MESMA data de hoje, com 20 minutos (queremos ver se o site soma ao registro existente ou cria uma linha nova). Salve e feche a janela.",
                    "Create one more entry on the SAME date (today), 20 minutes (we want to see whether the site adds to the existing record or creates a new row). Save and close the window.",
                    "Crea una entrada más en la MISMA fecha (hoy), 20 minutos (queremos ver si el sitio suma al registro existente o crea una fila nueva). Guarda y cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "journal_edit", StartUrl = Site,
                Title = new[] { "Editar uma entrada", "Edit an entry", "Editar una entrada" },
                Instruction = new[]
                {
                    "Edite a entrada de ontem do Hades: mude o tempo para 60 minutos e salve. Feche a janela.",
                    "Edit yesterday's Hades entry: change the time to 60 minutes and save. Close the window.",
                    "Edita la entrada de ayer de Hades: cambia el tiempo a 60 minutos y guarda. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "journal_delete", StartUrl = Site,
                Title = new[] { "Apagar as entradas de teste", "Delete the test entries", "Borrar las entradas de prueba" },
                Instruction = new[]
                {
                    "Apague as três entradas de teste que você criou no Hades, uma de cada vez. Feche a janela.",
                    "Delete the three test entries you created on Hades, one at a time. Close the window.",
                    "Borra las tres entradas de prueba que creaste en Hades, una por una. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "calendar", StartUrl = Site,
                Title = new[] { "Calendário do perfil", "Profile calendar", "Calendario del perfil" },
                Instruction = new[]
                {
                    "Abra o seu perfil e vá no Journal/Calendário. Navegue para o mês seguinte e depois para o anterior, e abra um dia que tenha atividade. Feche a janela.",
                    "Open your profile and go to the Journal/Calendar. Go to the next month and then the previous one, and open a day that has activity. Close the window.",
                    "Abre tu perfil y ve al Journal/Calendario. Ve al mes siguiente y luego al anterior, y abre un día con actividad. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "lists", StartUrl = Site,
                Title = new[] { "Listas de jogos do perfil", "Profile game lists", "Listas de juegos del perfil" },
                Instruction = new[]
                {
                    "Abra a lista de jogos do seu perfil (Played, Playing ou Backlog), role até carregar mais jogos e troque o filtro ou a ordenação uma vez. Feche a janela.",
                    "Open your profile game list (Played, Playing or Backlog), scroll until more games load and change the filter or sort once. Close the window.",
                    "Abre la lista de juegos de tu perfil (Played, Playing o Backlog), baja hasta que carguen más juegos y cambia el filtro u orden una vez. Cierra la ventana."
                }
            },
            new CaptureStep
            {
                Id = "cleanup", StartUrl = Site,
                Title = new[] { "Limpar o jogo de teste", "Clean up the test game", "Limpiar el juego de prueba" },
                Instruction = new[]
                {
                    "Confira a página do Hades e deixe o jogo sem status, sem nota e sem entradas no diário (use o que o site oferecer para remover). Feche a janela.",
                    "Check the Hades page and leave the game with no status, no rating and no journal entries (use whatever the site offers to remove them). Close the window.",
                    "Revisa la página de Hades y deja el juego sin estado, sin nota y sin entradas en el diario (usa lo que ofrezca el sitio para quitarlos). Cierra la ventana."
                }
            }
        };
    }
}
