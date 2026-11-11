using System.ComponentModel;

namespace BackloggdSync
{
    /// <summary>
    /// Estado do jogo selecionado, exposto para temas do Playnite (ex.: Dune YGP).
    /// O tema lê com {PluginSettings Plugin=BackloggdSync, Path=HasData} (e demais propriedades).
    /// </summary>
    public class ThemeState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _hasData;
        private bool _linked;
        private bool _ignored;
        private string _title = "";
        private int _pendingGame;
        private int _pendingTotal;
        private string _account = "";

        /// <summary>true = conta conectada e há um jogo único selecionado.</summary>
        public bool HasData { get { return _hasData; } private set { if (_hasData != value) { _hasData = value; Raise("HasData"); } } }

        /// <summary>true = este jogo já está vinculado a um jogo do Backloggd.</summary>
        public bool Linked { get { return _linked; } private set { if (_linked != value) { _linked = value; Raise("Linked"); } } }

        /// <summary>true = o usuário pediu para nunca enviar este jogo.</summary>
        public bool Ignored { get { return _ignored; } private set { if (_ignored != value) { _ignored = value; Raise("Ignored"); } } }

        /// <summary>Título do jogo no Backloggd (quando vinculado).</summary>
        public string Title { get { return _title; } private set { if (_title != value) { _title = value ?? ""; Raise("Title"); } } }

        /// <summary>Itens na fila deste jogo (tempo, status ou nota ainda não confirmados).</summary>
        public int PendingGame { get { return _pendingGame; } private set { if (_pendingGame != value) { _pendingGame = value; Raise("PendingGame"); } } }

        /// <summary>Itens na fila de todos os jogos.</summary>
        public int PendingTotal { get { return _pendingTotal; } private set { if (_pendingTotal != value) { _pendingTotal = value; Raise("PendingTotal"); } } }

        /// <summary>Usuário conectado no Backloggd.</summary>
        public string Account { get { return _account; } private set { if (_account != value) { _account = value ?? ""; Raise("Account"); } } }

        public void Update(bool connected, bool linked, bool ignored, string title, int pendingGame, int pendingTotal, string account)
        {
            Account = account;
            Title = title;
            PendingGame = pendingGame;
            PendingTotal = pendingTotal;
            Ignored = ignored;
            Linked = linked;
            HasData = connected;
        }

        public void Clear() { Update(false, false, false, "", 0, 0, ""); }

        private void Raise(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
