﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.RegularExpressions;


public class BLIO
{
    /// <summary>
    ///  Attempts to connect to the CommandInjector pipe.
    /// </summary>
    /// <returns>
    ///  The connected pipe, or null if it could not be connected to.
    /// </returns>
    /// <exception cref="System.IO.IOException" />
    /// 
    private static NamedPipeClientStream ConnectPipe()
    {
        var pipe = new NamedPipeClientStream(".", "BLCommandInjector", PipeDirection.InOut);
        if (!pipe.IsConnected)
            try
            {
                pipe.Connect(1000);
            }
            catch (TimeoutException)
            {
                return null;
            }
        if (pipe.IsConnected)
        {
            pipe.ReadMode = PipeTransmissionMode.Message;
            return pipe;
        }
        else
        {
            pipe.Close();
            return null;
        }
    }

    /// <summary>
    ///  Runs a command in the game console.
    /// </summary>
    /// <returns>
    ///  The lines of output from the command. If the command has no output, an
    ///  empty array is returned. If the command could not be run, returns null.
    /// </returns>
    /// <exception cref="System.ArgumentNullException" />
    /// <exception cref="System.FormatException" />
    /// 
    public static string[] RunCommand(string format, params object[] arguments)
    {
        string command = String.Format(format, arguments);
        List<string> results = new List<string>();

        // We will attempt to run the command up 3 times.
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            // If we can't even connect to the pipe, return null.
            var pipe = ConnectPipe();
            if (pipe == null)
                return null;

            // Create the reading and writing objects for the pipe.
            var pipeWriter = new StreamWriter(pipe);
            var pipeReader = new StreamReader(pipe);

            // While attempting to perform reads and writes, we will be catching
            // any IOExceptions that occur.
            try
            {
                // Write the line to the pipe, and flushing the pipe so that it
                // is transmitted fully.
                pipeWriter.WriteLine(command);
                pipeWriter.Flush();

                // Loop as we read from the pipe.
                for (; ; )
                {
                    string line = pipeReader.ReadLine();
                    // If reading returns null, we've reached the message's end.
                    if (line == null)
                        break;
                    // If there was data to read, add it to our results.
                    if (line != string.Empty)
                        results.Add(line);
                }
                //  If we've gotten this far without any exceptions, send 
                return results.ToArray();
            }
            catch (IOException exception)
            {
                //Console.WriteLine(exception);
            }
            pipe.Close();
        }
        return null;
    }

    // Performs a getall command for a given class and property, and returns a dictionary
    // with the property values keyed by their objects.
    public static Dictionary<string, string> GetAll(string className, string property)
    {
        // Create the dictionary we will return.
        var names = new Dictionary<string, string>();

        // Run the getall command, and iterate through the lines of the result.
        string[] namesDump = RunCommand("getall {0} {1}", className, property);
        foreach (string nameDump in namesDump)
        {
            // To extract the objects and values from the raw results, we locate their
            // markers in the command output.
            string prefix = $" {className} ";
            string suffix = $".{property} = ";
            int objectStart = nameDump.IndexOf(prefix);
            int objectEnd = nameDump.IndexOf(property);

            // If the line is missing either of the markers, ignore it.
            if (objectStart == -1 || objectEnd == -1)
                continue;

            // The object's name is immediately following the end of the "prefix," and
            // ends after the distance between that and the character before the suffix.
            objectStart += prefix.Length;
            objectEnd -= 1;
            string objectName = nameDump.Substring(objectStart, objectEnd - objectStart);

            // Exclude objects not starting using transient
            if (!objectName.Contains("Transient"))
            {
                continue;
            }

            // The object's property value begins just after the suffix, and ends after
            // the distance between that and the end of the line.
            int propertyStart = objectEnd + suffix.Length;
            names[objectName] = nameDump.Substring(propertyStart, nameDump.Length - propertyStart);
        }
        return names;
    }


    public class Object
    {
        public string Name;
        public string Class;

        // Lazily computed dictionary containing the player controller's properties.
        private Dictionary<string, string> _Properties;
        private Dictionary<string, string> Properties
        {
            get
            {
                if (_Properties != null)
                    return _Properties;

                // Create the dictionary we will return.
                _Properties = new Dictionary<string, string>();

                // Run the dump command, and iterate through the lines of the result.
                string[] propertiesDump = RunCommand("obj dump {0}", Name);
                foreach (string propertyDump in propertiesDump)
                {
                    // Find the index of the equals sign delineating the start of the value.
                    int propertyLength = propertyDump.IndexOf('=');
                    // If the equals sign is at the very beginning (or not found), this line
                    // does not actually contain a property dump.
                    if (propertyLength < 3)
                        continue;

                    // The name of the property starts at the beginning of the line, and
                    // ends at the location of the equals sign.
                    string property = propertyDump.Substring(2, propertyLength - 2);
                    // The value for the property is the entire remainder of the line
                    // after the equals sign.
                    _Properties[property] = propertyDump.Substring(propertyLength + 1);
                }
                return _Properties;
            }
        }

        public string GetProperty(string property)
        {
            // Attempt to retrieve the raw value of the property, returning null on failure.
            if (!Properties.TryGetValue(property, out string value))
                return null;

            return value;
        }
        public Object GetPropertyObject(string property)
        {
            // Attempt to retrieve the raw value of the property, returning null on failure.
            if (!Properties.TryGetValue(property, out string value))
                return null;
            return GetFromValue(value);
        }


        public string[] GetPropertyArray(string propertyName)
        {
            List<string> values = new List<string>();

            // Properties for members of the array will be in the following format:
            //     <property>(<index>)
            // Or:
            //     <property>[<index>]
            // Create a pattern to match these formats and capture the index.
            var propertyPattern = new Regex($@"^{Regex.Escape(propertyName)}([\[\(])(\d+)([\]\)])$", RegexOptions.Compiled);

            foreach (var property in Properties)
            {
                // Attempt to match the property against the pattern. If the match
                // failed, or if the index's brackets did not match, skip it.
                var match = propertyPattern.Match(property.Key);
                if (!match.Success ||
                    (match.Groups[1].Value == "(" && match.Groups[3].Value != ")") ||
                    (match.Groups[1].Value == "[" && match.Groups[3].Value != "]"))
                    continue;

                values.Add(property.Value);
            }

            // If no members of the array were found, consider it not to exist and
            // return null.
            return values.Count > 0 ? values.ToArray() : null;
        }

        public Object(string objectName)
        {
            Name = objectName;
            Class = null;
            _Properties = null;
        }

        public Object(string objectName, string className)
        {
            Name = objectName;
            Class = className;
            _Properties = null;
        }

        // Object declarations should be in the following format:
        //     <class name>'<object name>'
        private static Lazy<Regex> _ObjectRegex = new Lazy<Regex>(() => new Regex(@"^([^']+)'([^']+)'$", RegexOptions.Compiled));

        public static Object GetFromValue(string value)
        {
            if (value == null)
                return null;

            var match = _ObjectRegex.Value.Match(value);
            if (!match.Success)
                return null;

            string className = match.Groups[1].Value;
            string objectName = match.Groups[2].Value;

            return new Object(objectName, className);
        }

        public static Object GetPlayerController()
        {
            // Querying all LocalPlayer objects for their Actor property should
            // return a single object and its WillowPlayerController.
            var localPlayerControllers = GetAll("LocalPlayer", "Actor");
            // Iterate over said results, although we will be returning after the
            // first one, if any.
            foreach (string actor in localPlayerControllers.Values)
            {
                var controller = Object.GetFromValue(actor);
                if (controller != null)
                    return controller;
            }
            return null;
        }
    }
}
