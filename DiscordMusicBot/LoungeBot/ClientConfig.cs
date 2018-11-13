using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace DiscordMusicBot.LoungeBot
{
    internal static class ClientConfig
    {
        internal enum ConfigPriority
        {
            RuntimeChanges,
            FileChanges
        }
        /// <summary>
        /// Return the config file from directory
        /// </summary>
        internal static SerializedConfig FileConfig
        {
            get
            {
                return Deserialize(configFileName, new BinaryFormatter());
            }
        }
        internal const string configFileName = "LConfig.txt";
        internal static readonly string clientID;
        internal static readonly string clientSecret;
        internal static readonly string token;
        internal static SerializableField botName;
        internal static SerializableField serverName;
        internal static SerializableField textChannel;
        internal static SerializableField voiceChannel;
        static ClientConfig()
        {
            SerializedConfig config = FileConfig;
            clientID = config.clientID;
            clientSecret = config.clientSecret;
            token = config.token;
            botName = new SerializableField(config.botName);
            serverName = new SerializableField(config.serverName);
            textChannel = new SerializableField(config.textChannelName);
            voiceChannel = new SerializableField(config.voiceChannelName);
        }
        internal static void ApplyConfigFile(ConfigPriority priority = ConfigPriority.RuntimeChanges)
        {
            SerializedConfig config = FileConfig;
            if (clientID != config.clientID || clientSecret != config.clientSecret || token != config.token)
            {
                LogHelper.Logln($"One of the core values (ID, Secret, or Token) have been changed in the file ({Path.GetFullPath(configFileName)}). The program might need a restart to apply those new values.",
                    LogType.Warning);
            }
            if (SerializableField.needsToReserialize)
            {
                LogHelper.Logln("There are runtime changes to configuration.",LogType.Warning);
                if (priority == ConfigPriority.RuntimeChanges)
                {
                    LogHelper.Logln("Abort applying runtime configs from file configs.", LogType.Success);
                    return;
                }
                else if (priority == ConfigPriority.FileChanges)
                {
                    LogHelper.Logln("Discarding runtime changes to configs.", LogType.Warning);
                    SerializableField.Reset();
                }
            }
            botName = new SerializableField(config.botName);
            serverName = new SerializableField(config.serverName);
            textChannel = new SerializableField(config.textChannelName);
            voiceChannel = new SerializableField(config.voiceChannelName);
        }
        internal static void OnShutdown()
        {
            if (SerializableField.needsToReserialize)
            {
                new Thread(()=>Serialize(new SerializedConfig(clientID, clientSecret, botName.value, token,
                    serverName.value, textChannel.value, voiceChannel.value), configFileName, new BinaryFormatter())).Start();
            }
        }
        private static SerializedConfig Deserialize(string filePath, IFormatter formatter)
        {
            if (!File.Exists(filePath))
            {
                LogHelper.Logln("Trying to deserialize a non-existent file",LogType.Error);
                return null;
            }
            using (FileStream s = new FileStream(filePath, FileMode.Open))
            {
                return (SerializedConfig) formatter.Deserialize(s);
            }
        }
        private static void Serialize(SerializedConfig config,string filePath, IFormatter formatter)
        {
            using (FileStream s = new FileStream(filePath, FileMode.Create))
            {
                formatter.Serialize(s, config);
                s.Close();
            }
        }
    }
    internal class SerializableField
    {
        internal static bool needsToReserialize = false;
        internal static void Reset()
        {
            needsToReserialize = false;
        }
        internal SerializableField(string defaultValue)
        {
            this.defaultValue = defaultValue;
        }
        private readonly string defaultValue;
        internal string value;
        internal void ChangeValue(string newValue)
        {
            if (newValue != defaultValue)
            {
                needsToReserialize = true;
                value = newValue;
            }
        }
    }
    [Serializable]
    public class SerializedConfig : ISerializable
    {
        public readonly string clientID;
        public readonly string clientSecret;
        public readonly string botName;
        public readonly string token;
        public readonly string serverName;
        public readonly string textChannelName;
        public readonly string voiceChannelName;

        public readonly static SerializedConfig Nameless = new SerializedConfig(
            id: "507310750801592330",
            secret: "UNIMPLEMENTED",
            botName: "Nameless",
            token: "NTA3MzEwNzUwODAxNTkyMzMw.Dru3Pw.6SqMzuFglO_NnUsOxQkQWK98VRs",
            serverName: "devv",
            textChannelName: "bot-console",
            voiceChannelName: "General"
            );
        public SerializedConfig(string id, string secret, string botName, 
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
        public SerializedConfig(SerializedConfig other)
        {
            this.clientID = other.clientID;
            this.clientSecret = other.clientSecret;
            this.token = other.token;
            this.serverName = other.serverName;
            this.textChannelName = other.textChannelName;
            this.voiceChannelName = other.voiceChannelName;
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
        #region Serialization/Deserialization
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("client_id", clientID);
            info.AddValue("client_secret", clientSecret);
            info.AddValue("bot_name", botName);
            info.AddValue("client_token", token);
            info.AddValue("auto_connect_server", serverName);
            info.AddValue("auto_connect_text_channel", textChannelName);
            info.AddValue("auto_conntect_voice_channel", voiceChannelName);
        }
        public SerializedConfig(SerializationInfo info, StreamingContext context)
        {
            clientID = info.GetString("client_id");
            clientSecret = info.GetString("client_secret");
            botName = info.GetString("bot_name");
            token = info.GetString("client_token");
            serverName = info.GetString("auto_connect_server");
            textChannelName = info.GetString("auto_connect_text_channel");
            voiceChannelName = info.GetString("auto_connect_voice_channel");

        }
        #endregion
    }
}
