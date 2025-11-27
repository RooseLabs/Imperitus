using Newtonsoft.Json.Linq;

namespace RooseLabs.Vosk
{
    public class RecognitionResult
    {
        private const string AlternativesKey = "alternatives";
        private const string ResultKey = "result";
        private const string PartialKey = "partial";

        public RecognizedPhrase[] Phrases { get; }
        public bool Partial { get; private set; }

        public RecognitionResult(string json)
        {
            JObject resultJson = JObject.Parse(json);

            if (resultJson.TryGetValue(AlternativesKey, out var alternativesToken))
            {
                var alternatives = (JArray)alternativesToken;
                Phrases = new RecognizedPhrase[alternatives.Count];
                for (int i = 0; i < Phrases.Length; i++)
                {
                    Phrases[i] = new RecognizedPhrase(alternatives[i] as JObject);
                }
            }
            else if (resultJson.ContainsKey(ResultKey))
            {
                Phrases = new RecognizedPhrase[] { new(resultJson) };
            }
            else if (resultJson.TryGetValue(PartialKey, out var value))
            {
                Partial = true;
                Phrases = new RecognizedPhrase[] { new(value.ToString())};
            }
            else
            {
                Phrases = new[] { new RecognizedPhrase() };
            }
        }
    }

    public class RecognizedPhrase
    {
        private const string ConfidenceKey = "confidence";
        private const string TextKey = "text";

        public string Text { get; private set; } = string.Empty;
        public float Confidence { get; private set; } = 0f;

        public RecognizedPhrase() { }

        public RecognizedPhrase(string text, float confidence = 0f)
        {
            Text = text;
            Confidence = confidence;
        }

        public RecognizedPhrase(JObject json)
        {
            if (json.ContainsKey(ConfidenceKey))
            {
                Confidence = json[ConfidenceKey].Value<float>();
            }

            if (json.ContainsKey(TextKey))
            {
                // Vosk adds an extra space at the start of the string.
                Text = json[TextKey].Value<string>().Trim();
            }
        }
    }
}
