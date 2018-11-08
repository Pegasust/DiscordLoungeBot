using Newtonsoft.Json;
using System.IO;

namespace DiscordMusicBot {
    internal static class Information {
        internal static Config Config => JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));

        internal static string ClientId => Config.ClientId;
        internal static string ClientSecret => Config.ClientSecret;
        internal static string BotName => Config.BotName;
        internal static string Token => Config.Token;
        internal static string ServerName => Config.ServerName;
        internal static string TextChannelName => Config.TextChannelName;
        internal static string VoiceChannelName => Config.VoiceChannelName;
    }

    public class Config {
        public string ClientId = "507310750801592330";
        public string ClientSecret = "YourClientSecret";
        public string BotName = "Lounge Bot";
        public string Token = "NTA3MzEwNzUwODAxNTkyMzMw.Dru3Pw.6SqMzuFglO_NnUsOxQkQWK98VRs";
        public string ServerName = "devv";
        public string TextChannelName = "bot-console";
        public string VoiceChannelName = "General";

        public static bool operator ==(Config cfg1, Config cfg2) {
            return cfg1 is null ? cfg2 is null : cfg1.Equals(cfg1);
        }

        public static bool operator !=(Config cfg1, Config cfg2) {
            return !ReferenceEquals(cfg1, null) ? !ReferenceEquals(cfg2, null) : !cfg1.Equals(cfg2);
        }

        public bool Equals(Config compare) {
            if (compare == null)
                return false;

            return
                ClientId == compare.ClientId &&
                ClientSecret == compare.ClientSecret &&
                BotName == compare.BotName &&
                Token == compare.Token &&
                ServerName == compare.ServerName &&
                TextChannelName == compare.TextChannelName &&
                VoiceChannelName == compare.VoiceChannelName;
        }

        public override bool Equals(object obj) {
            return Equals(obj as Config);
        }

        public override int GetHashCode() {
            return (ClientId + ClientSecret + BotName + Token + ServerName + TextChannelName + VoiceChannelName).GetHashCode();
        }
    }
}