using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
namespace DiscordMusicBot.LoungeBot
{
    class ClientConfig
    {
        private string configFileName = "lconfig.txt";
        public string m_ConfigFileName
        {
            get
            {
                return configFileName;
            }
            set
            {
                Rename(value);
                configFileName = value;
            }
        }
        private void Rename(string newName)
        {
            File.Move(configFileName, newName);
        }

    }

    [Serializable]
    internal struct SerializedConfig
    {
        internal readonly string clientID;
        internal readonly string clientSecret;
        internal readonly string botName;
        internal readonly string token;
        internal readonly string serverName;
        internal readonly string textChannelName;
        internal readonly string voiceChannelName;

        internal readonly static SerializedConfig Nameless = new SerializedConfig(
            id: "507310750801592330",
            secret: "UNIMPLEMENTED",
            botName: "Nameless",
            token: "NTA3MzEwNzUwODAxNTkyMzMw.Dru3Pw.6SqMzuFglO_NnUsOxQkQWK98VRs",
            serverName: "devv",
            textChannelName: "bot-console",
            voiceChannelName: "General"
            );
        internal SerializedConfig(string id, string secret, string botName, 
            string token, string serverName,
            string textChannelName, string voiceChannelName)
        {
            clientID = id;
            clientSecret = secret;
            this.botName = botName;
            this.token = token;
            this.serverName = serverName;
            this.textChannelName = textChannelName;
            this.voiceChannelName = voiceChannelName;
        }
        internal SerializedConfig(SerializedConfig other)
        {
            this = other;
        }
        public static bool operator ==(SerializedConfig lhs, SerializedConfig rhs)
        {
            return lhs.Equals(rhs);
        }
        public static bool operator !=(SerializedConfig lhs, SerializedConfig rhs)
        {
            return !lhs.Equals(rhs);
        }
        public bool Equals(SerializedConfig compare)
        {
            if (compare == null)
            {
                return false;
            }
            return
                clientID == compare.clientID &&
                clientSecret == compare.clientSecret &&
                botName == compare.botName &&
                token == compare.token &&
                serverName == compare.serverName &&
                textChannelName == compare.textChannelName &&
                voiceChannelName == compare.voiceChannelName;
        }
        public override bool Equals(object obj)
        {
            return Equals((SerializedConfig) obj);
        }
        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(clientID);
            hash.Add(clientSecret);
            hash.Add(botName);
            hash.Add(token);
            hash.Add(serverName);
            hash.Add(textChannelName);
            hash.Add(voiceChannelName);
            return hash.ToHashCode();
        }
    }
}
