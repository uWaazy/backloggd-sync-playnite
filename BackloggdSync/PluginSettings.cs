using System;
using System.Collections.Generic;
using System.ComponentModel;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace BackloggdSync
{
    /// <summary>Vínculo entre um jogo do Playnite e o jogo do Backloggd.</summary>
    public class GameLink
    {
        public int BackloggdId { get; set; }
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>true = o usuário pediu para nunca enviar este jogo.</summary>
        public bool Ignored { get; set; }
        /// <summary>Último status/nota enviados: só reenviamos quando o valor no Playnite muda.</summary>
        public string LastStatus { get; set; } = "";
        public int LastRating { get; set; }
    }

    /// <summary>Trabalho pendente: tempo de um dia e/ou conferência de status e nota.</summary>
    public class PendingItem
    {
        public string GameId { get; set; } = "";
        /// <summary>Dia local (yyyy-MM-dd) ou vazio quando só há status/nota para conferir.</summary>
        public string Day { get; set; } = "";
        public int AddMinutes { get; set; }
        /// <summary>Valor final do dia, gravado antes do primeiro envio para que o reenvio nunca some duas vezes.</summary>
        public int? Target { get; set; }
        /// <summary>true = não achamos o jogo no Backloggd com certeza; o item fica parado até o jogo ser vinculado.</summary>
        public bool NeedsLink { get; set; }
        public int Attempts { get; set; }
        public string LastError { get; set; } = "";
    }

    /// <summary>Configurações do plugin.</summary>
    public class PluginSettings : ISettings, IEditableObject
    {
        private Plugin _owner;

        public void AttachOwner(Plugin owner) { _owner = owner; }

        public void Save() { if (_owner != null) _owner.SavePluginSettings(this); }

        /// <summary>Idioma da interface: "auto" (segue o Playnite), "pt", "en" ou "es".</summary>
        public string Language { get; set; } = "auto";

        public string Username { get; set; } = "";
        public int UserId { get; set; }

        public bool AutoSyncOnStop { get; set; } = true;
        public bool SyncPlaytime { get; set; } = true;
        public bool SyncStatus { get; set; } = true;
        public bool SyncRating { get; set; } = true;

        public Dictionary<string, GameLink> Links { get; set; } = new Dictionary<string, GameLink>();
        public List<PendingItem> Pending { get; set; } = new List<PendingItem>();

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public void BeginEdit() { }

        public void EndEdit() { Save(); }

        public void CancelEdit() { }
    }
}
