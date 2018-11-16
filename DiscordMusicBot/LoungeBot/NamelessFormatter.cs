using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;

namespace DiscordMusicBot.LoungeBot
{
    class NamelessFormatter : IFormatter
    {
        public SerializationBinder binder;
        public StreamingContext context;
        public ISurrogateSelector surrogateSelector;
        public NamelessFormatter()
        {
            context = new StreamingContext(StreamingContextStates.All);
        }
        public object Deserialize(Stream serializationStream)
        {
            StreamReader sr = new StreamReader(serializationStream);
            //Get type of class
            string classLine = sr.ReadLine();
            //Confirm that the file is serialized using NamelessFormatter
            int h = 0;
            for (; h < ClassIdentifier.Length; h++)
            {
                if (classLine[h] != ClassIdentifier[h])
                {
                    //NOT THIS FORMAT!
                    throw new System.ArgumentException("Attempting to deserialize a file not formatted by Nameless.");
                }
            }
            //This file was serialized by NamelessFormatter
            string className = classLine.Substring(h);
            Type classType = Type.GetType(className);
            //Get total properties
            int totalProperties;
            int.TryParse(sr.ReadLine(), out totalProperties);
            //Read fields
            string[] strArr;
            Dictionary<string, string> sDict = new Dictionary<string, string>(totalProperties);
            for (int i = 0; i < totalProperties; i++)
            {
                strArr = sr.ReadLine().Split('=');
                sDict[strArr[0].Trim()] = strArr[1].Trim();
            }
            sr.Close();
            MemberInfo[] members = FormatterServices.GetSerializableMembers(classType, context);
            object[] objs = new object[totalProperties];
            for (;--totalProperties >= 0;)
            {
                FieldInfo fi = (FieldInfo)members[totalProperties];
                if (!sDict.ContainsKey(fi.Name))
                {
                    throw new SerializationException("Missing field value: " + fi.Name);
                }
                objs[totalProperties] = System.Convert.ChangeType(sDict[fi.Name], fi.FieldType);
            }
            object obj = FormatterServices.GetUninitializedObject(classType);
            return FormatterServices.PopulateObjectMembers(obj, members, objs);
        }
        public void Serialize(Stream serializationStream, object graph)
        {
            //Serializable
            MemberInfo[] members = FormatterServices.GetSerializableMembers(graph.GetType(), context);
            //Data from fields
            object[] objs = FormatterServices.GetObjectData(graph, members);
            using (StreamWriter sw = new StreamWriter(serializationStream))
            {
                //Class name
                sw.WriteLine(ClassFormat(graph.GetType().FullName));
                //Total fields
                sw.WriteLine(members.Length.ToString());
                //Other fields
                for (int i = 0; i < objs.Length; i++)
                {
                    sw.WriteLine($"{members[i].Name}={objs[i].ToString()}");
                }
            }            
            
        }
        private string ClassFormat(string className)
        {
            return ClassIdentifier + className;
        }

        private const string ClassIdentifier = "»Class=";

        public SerializationBinder Binder
        {
            get => binder;
            set => binder = value;
        }
        public StreamingContext Context
        {
            get => context;
            set => context = value;
        }
        public ISurrogateSelector SurrogateSelector
        {
            get => surrogateSelector;
            set => surrogateSelector = value;
        }
    }
}
