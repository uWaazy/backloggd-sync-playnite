using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BackloggdSync
{
    public class StepInfo
    {
        public bool Done;
        public int RequestCount;
        public string FinishedLocal = "";
    }

    /// <summary>Guarda os resultados de cada passo em arquivos na pasta de dados do plugin e gera o arquivo final para envio.</summary>
    public class CaptureStore
    {
        public const string ExportName = "backloggd-capture.json";

        public string Folder { get; private set; }

        public CaptureStore(string dataPath)
        {
            Folder = Path.Combine(dataPath ?? Path.GetTempPath(), "capture");
        }

        private string StepFile(int index, string id)
        {
            return Path.Combine(Folder, string.Format("step-{0:00}-{1}.json", index + 1, id));
        }

        public string ExportPath { get { return Path.Combine(Folder, ExportName); } }

        public StepInfo GetInfo(int index, CaptureStep step)
        {
            var info = new StepInfo();
            try
            {
                var file = StepFile(index, step.Id);
                if (!File.Exists(file)) return info;

                var json = JObject.Parse(File.ReadAllText(file));
                info.Done = true;
                info.RequestCount = (int?)json["requestCount"] ?? 0;
                DateTime finished;
                if (DateTime.TryParse((string)json["finishedUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind, out finished))
                    info.FinishedLocal = finished.ToLocalTime().ToString("dd/MM HH:mm");
            }
            catch
            {
                info.Done = false;
            }
            return info;
        }

        public void Save(int index, CaptureStep step, JObject result)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(StepFile(index, step.Id), result.ToString(Formatting.Indented));
        }

        /// <summary>Junta todos os passos feitos em um único arquivo. Devolve o caminho, ou null se não houver nada.</summary>
        public string Export(string pluginVersion)
        {
            if (!Directory.Exists(Folder)) return null;

            var steps = new JArray();
            for (int i = 0; i < CaptureSteps.All.Count; i++)
            {
                var file = StepFile(i, CaptureSteps.All[i].Id);
                if (!File.Exists(file)) continue;
                steps.Add(JObject.Parse(File.ReadAllText(file)));
            }

            if (steps.Count == 0) return null;

            var export = new JObject
            {
                ["tool"] = "Backloggd Sync capture mode",
                ["pluginVersion"] = pluginVersion,
                ["generatedUtc"] = DateTime.UtcNow.ToString("o"),
                ["steps"] = steps
            };

            File.WriteAllText(ExportPath, export.ToString(Formatting.Indented));
            return ExportPath;
        }

        public void Clear()
        {
            if (!Directory.Exists(Folder)) return;
            foreach (var f in Directory.GetFiles(Folder, "*.json")) File.Delete(f);
        }
    }
}
