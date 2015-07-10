﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public enum IniFiles
    {
        GameUserSettings,
        Game
    }

    public enum IniFileSections
    {
        ServerSettings,
        SessionSettings,
        GameSession,
        GameMode,
        MessageOfTheDay,
        MultiHome
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple=true)]
    public class IniFileEntryAttribute : Attribute
    {
        public IniFiles File;

        /// <summary>
        /// The section of the ini file.
        /// </summary>
        public IniFileSections Section;

        /// <summary>
        /// The key within the section.
        /// </summary>
        public string Key;

        /// <summary>
        /// If true, the value of booleans will be inverted when read or written.
        /// </summary>
        public bool InvertBoolean;

        /// <summary>
        /// If true, will also write a true boolean value when the underlying field is non-default (or empty for strings), otherwise a false value will be written.
        /// </summary>
        public bool WriteBoolValueIfNonEmpty;

        /// <summary>
        /// Clear the section before writing this value.
        /// </summary>
        public bool ClearSection;

        /// <summary>
        /// Only write the attributed value if the named field is true.
        /// </summary>
        public string ConditionedOn;
        
        /// <summary>
        /// Attribute for the IniFile serializer
        /// </summary>
        /// <param name="File">The file into which the setting should be serialized.</param>
        /// <param name="Section">The section in the ini file.</param>
        /// <param name="Key">The key within the section.  Defaults to the same name as the attributed field.</param>
        public IniFileEntryAttribute(IniFiles File, IniFileSections Section, string Key = "")
        {
            this.File = File;
            this.Section = Section;
            this.Key = Key;
        }
    }

    /// <summary>
    /// Class for reading/writing INI files
    /// </summary>
    /// <remarks>
    /// From http://www.codeproject.com/Articles/1966/An-INI-file-handling-class-using-C
    /// </remarks>
    class IniFile
    {
        private readonly Dictionary<IniFileSections, string> SectionNames = new Dictionary<IniFileSections, string>()
        {
            { IniFileSections.GameMode, "/script/shootergame.shootergamemode" },
            { IniFileSections.GameSession, "/Script/Engine.GameSession"},
            { IniFileSections.MessageOfTheDay, "MessageOfTheDay" },
            { IniFileSections.MultiHome, "MultiHome" },
            { IniFileSections.ServerSettings, "ServerSettings" },
            { IniFileSections.SessionSettings, "SessionSettings" },
        };

        private readonly Dictionary<IniFiles, string> FileNames = new Dictionary<IniFiles, string>()
        {
            { IniFiles.GameUserSettings, "GameUserSettings.ini" },
            { IniFiles.Game, "Game.ini" }
        };

        public string basePath;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section,
            string key, string val, string filePath);


        [DllImport("kernel32")]
        private static extern int WritePrivateProfileSection(string section, string data, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section,
                 string key, string def, StringBuilder retVal,
            int size, string filePath);

        [DllImport("kernel32", CharSet=CharSet.Auto)]
        private static extern int GetPrivateProfileSection(string section, char[] retVal, int size, string filePath);

        /// <summary>
        /// INIFile Constructor.
        /// </summary>
        /// <PARAM name="INIPath"></PARAM>
        public IniFile(string INIPath)
        {
            basePath = INIPath;
        }

        /// <summary>
        /// Writes the specified object's fields to the INI file, based on the field attributes.
        /// </summary>
        /// <param name="obj"></param>
        public void Serialize(object obj)
        {
            var fields = obj.GetType().GetFields().Where(f => f.IsDefined(typeof(IniFileEntryAttribute), false));
            foreach(var field in fields)
            {
                var attributes = field.GetCustomAttributes(typeof(IniFileEntryAttribute), false);   
                foreach(var attribute in attributes)
                {
                    var attr = attribute as IniFileEntryAttribute;
                    var value = field.GetValue(obj);
                    var keyName = String.IsNullOrWhiteSpace(attr.Key) ? field.Name : attr.Key;

                    if(attr.ClearSection)
                    {
                        IniWriteValue(attr.Section, null, null, IniFiles.GameUserSettings);
                    }


                    if(!String.IsNullOrEmpty(attr.ConditionedOn))
                    {
                        var conditionField = obj.GetType().GetField(attr.ConditionedOn);
                        var conditionValue = conditionField.GetValue(obj);
                        if(conditionValue is bool && (bool)conditionValue == false)
                        {
                            // The condition value was not set to true, so clear this attribute instead of writing it
                            IniWriteValue(attr.Section, keyName, null, attr.File);
                            continue;
                        }
                    }

                    if(attr.WriteBoolValueIfNonEmpty)
                    {
                        if(value == null)
                        {
                            IniWriteValue(attr.Section, keyName, "False", attr.File);
                        }
                        else
                        {                           
                            if(value is string)
                            {
                                var strValue = value as string;
                                IniWriteValue(attr.Section, keyName, String.IsNullOrEmpty(strValue) ? "False" : "True", attr.File);
                            }
                            else
                            {
                                // Not supported
                                throw new NotSupportedException("Unexpected IniFileEntry value type.");
                            }                            
                        }
                    }
                    else
                    {
                        if (attr.InvertBoolean && value is Boolean)
                        {
                            IniWriteValue(attr.Section, keyName, Convert.ToString(!(bool)(value)), attr.File);
                        }
                        else
                        {
                            IniWriteValue(attr.Section, keyName, Convert.ToString(value), attr.File);
                        }
                    }
                }
            }
        }

        public void Deserialize(object obj)
        {
            var fields = obj.GetType().GetFields().Where(f => f.IsDefined(typeof(IniFileEntryAttribute), false));
            foreach (var field in fields)
            {
                var attributes = field.GetCustomAttributes(typeof(IniFileEntryAttribute), false);
                bool extraBoolValue = false;
                foreach (var attribute in attributes)
                {
                    var attr = attribute as IniFileEntryAttribute;
                    var keyName = String.IsNullOrWhiteSpace(attr.Key) ? field.Name : attr.Key;

                    if (attr.WriteBoolValueIfNonEmpty)
                    {
                        // Don't really need to do anything here, we don't care about this on reading it.
                        // extraBoolValue = Convert.ToBoolean(IniReadValue(SectionNames[attr.Section], attr.Key));
                    }
                    else
                    {
                        var iniValue = IniReadValue(SectionNames[attr.Section], keyName, FileNames[attr.File]);
                        var fieldType = field.FieldType;

                        if (fieldType == typeof(string))
                        {
                            field.SetValue(obj, iniValue);
                        }
                        else
                        {
                            // Update the ConditionedOn flag, if this field has one.
                            if (!String.IsNullOrWhiteSpace(attr.ConditionedOn))
                            {
                                var conditionField = obj.GetType().GetField(attr.ConditionedOn);
                                if (String.IsNullOrWhiteSpace(iniValue))
                                {
                                    conditionField.SetValue(obj, false);
                                }
                                else
                                {
                                    conditionField.SetValue(obj, true);
                                }
                            }

                            if (String.IsNullOrWhiteSpace(iniValue))
                            {                                

                                // Skip non-string values which are not found
                                continue;
                            }

                            if (fieldType == typeof(bool))
                            {
                                var boolValue = false;
                                bool.TryParse(iniValue, out boolValue);
                                if (attr.InvertBoolean)
                                {
                                    boolValue = !boolValue;
                                }

                                field.SetValue(obj, boolValue);
                            }
                            else if (fieldType == typeof(int))
                            {
                                int intValue;
                                int.TryParse(iniValue, out intValue);
                                field.SetValue(obj, intValue);
                            }
                            else if (fieldType == typeof(float))
                            {
                                float floatValue;
                                float.TryParse(iniValue, out floatValue);
                                field.SetValue(obj, floatValue);
                            }
                            else
                            {
                                throw new ArgumentException(String.Format("Unexpected field type {0} for INI key {1} in section {2}.", fieldType.ToString(), attr.Key, attr.Section));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Write Data to the INI File
        /// </summary>
        /// <PARAM name="Section"></PARAM>
        /// Section name
        /// <PARAM name="Key"></PARAM>
        /// Key Name
        /// <PARAM name="Value"></PARAM>
        /// Value Name
        public void IniWriteValue(string Section, string Key, string Value, string pathSuffix = "")
        {
            WritePrivateProfileString(Section, Key, Value, Path.Combine(this.basePath, pathSuffix));
        }

        public void IniWriteValue(IniFileSections Section, string Key, string Value, IniFiles File)
        {
            IniWriteValue(SectionNames[Section], Key, Value, FileNames[File]);
        }

        /// <summary>
        /// Read Data Value From the Ini File
        /// </summary>
        /// <PARAM name="Section"></PARAM>
        /// <PARAM name="Key"></PARAM>
        /// <PARAM name="Path"></PARAM>
        /// <returns></returns>
        public string IniReadValue(string Section, string Key, string pathSuffix = "")
        {
            const int MaxValueSize = 2048;
            StringBuilder temp = new StringBuilder(MaxValueSize);
            int i = GetPrivateProfileString(Section, Key, "", temp,
                                            MaxValueSize, Path.Combine(this.basePath, pathSuffix));
            return temp.ToString();
        }

        public string[] IniReadSection(IniFileSections Section, IniFiles File)
        {
            return IniReadSection(SectionNames[Section], FileNames[File]);
        }

        public string[] IniReadSection(string Section, string pathSuffix = "")
        {
            const int MaxSectionSize = 65536;
            var temp = new char[MaxSectionSize];
            int i = GetPrivateProfileSection(Section, temp, MaxSectionSize, Path.Combine(this.basePath, pathSuffix));
            return MultiStringToArray(temp);
        }

        public void IniWriteSection(IniFileSections Section, string[] values, IniFiles File)
        {
            IniWriteSection(SectionNames[Section], values, FileNames[File]);
        }

        public void IniWriteSection(string Section, string[] values, string pathSuffix = "")
        {
            WritePrivateProfileSection(Section, StringArrayToMultiString(values), Path.Combine(this.basePath, pathSuffix));
        }

        static string StringArrayToMultiString(params string[] values)
        {
            if (values == null) throw new ArgumentNullException("values");
            StringBuilder multiString = new StringBuilder();

            if (values.Length == 0)
            {
                multiString.Append('\0');
            }
            else
            {
                foreach (string s in values)
                {
                    multiString.Append(s);
                    multiString.Append('\0');
                }
            }
            return multiString.ToString();
        }

        static string[] MultiStringToArray(char[] multistring)
        {
            List<string> stringList = new List<string>();
            int i = 0;
            while (i < multistring.Length)
            {
                int j = i;
                if (multistring[j++] == '\0') break;
                while (j < multistring.Length)
                {
                    if (multistring[j++] == '\0')
                    {
                        stringList.Add(new string(multistring, i, j - i - 1));
                        i = j;
                        break;
                    }
                }
            }

            return stringList.ToArray();
        }
    }
}
