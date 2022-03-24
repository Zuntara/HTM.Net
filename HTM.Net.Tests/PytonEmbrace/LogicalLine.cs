using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.PytonEmbrace
{
    public class LogicalLine
    {
        private int _lineNumber;
        private StringBuilder _text;

        public string Text
        {
            get
            {
                if (_lastSignificantCharIndex != -1)
                {
                    string significantText = _text.ToString(0, _lastSignificantCharIndex + 1);
                    string insignificantText = _text.ToString(significantText.Length, _text.Length - significantText.Length);

                    if (_text[_lastSignificantCharIndex] == ':')
                    {
                        // Block start
                        return ConvertBlockLine(significantText) + insignificantText;
                    }
                    else
                    {
                        // Add a semicolon
                        return significantText + ";" + insignificantText;
                    }
                }
                else
                {
                    // Entirely whitespace, or just a comment
                    return _text.ToString();
                }
            }
        }

        private static readonly string[] BracketedKeywords = { "if", "elif", "for", "while", "except" };

        private static readonly Regex BlockLinePattern =
            new Regex(@"^(\s*)(if|else|elif|def|for|while|try|except|class|finally)(.*):$",
                RegexOptions.Compiled | RegexOptions.Singleline);

        private string ConvertBlockLine(string significantText)
        {
            if (BlockLinePattern.IsMatch(significantText))
            {
                // FIXME remove colon and brackets if necessary
                Match match = BlockLinePattern.Match(significantText);

                string whitespace = match.Groups[1].Value;
                string keyword = match.Groups[2].Value;
                string remainder = match.Groups[3].Value;

                bool needsBrackets = BracketedKeywords.Contains(keyword) && !remainder.TrimStart().StartsWith("(");

                if (keyword == "elif")
                {
                    keyword = "else if";
                }
                else if (keyword == "except")
                {
                    keyword = "catch";
                }

                return whitespace + keyword + (needsBrackets ? "(" : "") + remainder + (needsBrackets ? ")" : "");

            }
            else
            {
                throw new Exception("Line " + _lineNumber + " ends with a : but doesn't match BlockLinePattern");
            }
        }

        public LogicalLine(int lineNumber, string physicalLine)
        {
            _lineNumber = lineNumber;
            _text = new StringBuilder(physicalLine.TrimEnd());
            Parse();
        }

        public void Append(string physicalLine)
        {
            Debug.Assert(!IsComplete);
            _text.AppendLine();
            _text.Append(physicalLine.TrimEnd());
            Parse();
        }

        public int IndentDepth
        {
            get
            {
                int i = 0;
                while (i < _text.Length && Char.IsWhiteSpace(_text[i]))
                {
                    i++;
                }
                return i;
            }
        }

        private bool EndsWithLineContinuationCharacter
        {
            get { return _text.Length != 0 && _text[_text.Length - 1] == '\\'; }
        }


        public bool IsComplete
        {
            get { return _bracketBalance == 0 && (!EndsWithLineContinuationCharacter) && (!_unterminatedTripleQuotedString); }
        }

        public bool IsBlank
        {
            get { return IndentDepth == _text.Length; }
        }


        // Characters considered for implicit line continuation - see Python language spec 2.1.6
        private static readonly char[] Opening = new char[] { '{', '(', '[' };
        private static readonly char[] Closing = new char[] { '}', ')', ']' };

        private int _bracketBalance;
        private int _lastSignificantCharIndex; // Index of the last non-whitespace character before any EOL comment
        private bool _unterminatedTripleQuotedString;

        private void Parse()
        {
            _bracketBalance = 0;
            _lastSignificantCharIndex = -1;

            bool inString = false;
            bool inComment = false;
            char stringStart = '\0';
            char prev = '\0';

            for (int i = 0; i < _text.Length; i++)
            {
                char c = _text[i];

                if (inComment)
                {
                    if (c == '\n')
                    {
                        inComment = false;
                    }
                }
                else if (!inString)
                {
                    if (c == '#')
                    {
                        inComment = true;
                    }
                    else if (!Char.IsWhiteSpace(c))
                    {
                        _lastSignificantCharIndex = i;

                        if (c == '"' || c == '\'')
                        {
                            // Start of normal string, or triple quoted string
                            if (SkipTripleQuotedString(ref i))
                            {
                                _lastSignificantCharIndex = i;
                            }
                            else
                            {
                                // it is a normal string
                                inString = true;
                                stringStart = c;
                            }
                        }
                        else if (Opening.Contains(c))
                        {
                            _bracketBalance++;
                        }
                        else if (Closing.Contains(c))
                        {
                            _bracketBalance--;
                        }
                    }
                }
                else
                {
                    if (c == stringStart && prev != '\\')
                    {
                        inString = false;
                        _lastSignificantCharIndex = i;
                    }
                }

                if (c == '\\' && prev == '\\')
                {
                    prev = '\0';
                }
                else
                {
                    prev = c;
                }
            }
        }


        private bool SkipTripleQuotedString(ref int i)
        {
            char startChar = _text[i];

            Debug.Assert(startChar == '\'' || startChar == '"');

            // if next two characters are the same, then we have a triple quoted string
            if (TripleQuotesAtIndex(startChar, i))
            {
                i += 3; // Skip opening

                _unterminatedTripleQuotedString = true;

                while (i < _text.Length)
                {
                    if (TripleQuotesAtIndex(startChar, i))
                    {
                        _unterminatedTripleQuotedString = false;
                        i += 2; // Skip to last char of closing
                        break;
                    }
                    i++;
                }

                return true;
            }
            else
            {
                return false; // No string to skip
            }
        }

        private bool TripleQuotesAtIndex(char startChar, int i)
        {
            return _text.Length > i + 2
                && _text[i + 0] == startChar
                && _text[i + 1] == startChar
                && _text[i + 2] == startChar;
        }

    }

    public static class PythonConverter
    {
        public static string ConvertString(string pythonCode)
        {
            return Convert(pythonCode);
        }

        public static void ConvertFile(string pythonFilePath)
        {
            string outputFilePath =
                Path.Combine(
                    Path.GetDirectoryName(pythonFilePath),
                    Path.GetFileNameWithoutExtension(pythonFilePath) + ".cs"
                );

            File.WriteAllText(outputFilePath, Convert(File.ReadAllText(pythonFilePath)));
        }

        private static string Convert(string pythonCode)
        {
            StringBuilder output = new StringBuilder();

            Stack<int> indentStack = new Stack<int>(new[] { 0 });

            LogicalLine currentLine = null;

            int lineNumber = 0;

            int blankLineCount = 0;

            foreach (string physicalLine in pythonCode.Split('\n'))
            {
                lineNumber++;

                if (currentLine == null || currentLine.IsComplete)
                {
                    currentLine = new LogicalLine(lineNumber, physicalLine);
                }
                else
                {
                    currentLine.Append(physicalLine);
                }

                if (currentLine.IsComplete)
                {
                    if (currentLine.IsBlank)
                    {
                        // Blank lines have no effect on indentation levels
                        // Store them up, rather than rendering inline, because we want them to appear after any braces
                        blankLineCount++;
                    }
                    else
                    {
                        if (currentLine.IndentDepth > indentStack.First())
                        {
                            output.AppendLine("{".PadLeft(indentStack.First() + 1));
                            indentStack.Push(currentLine.IndentDepth);
                        }
                        else
                        {
                            while (currentLine.IndentDepth != indentStack.First())
                            {
                                indentStack.Pop();
                                output.AppendLine("}".PadLeft(indentStack.First() + 1));
                            }
                        }

                        while (blankLineCount > 0)
                        {
                            output.AppendLine();
                            blankLineCount--;
                        }

                        output.AppendLine(currentLine.Text);
                    }
                }
            }

            // Close off braces at end of file
            while (indentStack.Count > 1)
            {
                indentStack.Pop();
                output.AppendLine("}".PadLeft(indentStack.First() + 1));
            }

            return output.ToString();
        }

    }

    // [TestClass]
    public class ConvertPytonTest
    {
        //[TestMethod]
        public void ConvertFile()
        {
            string fileToConvert = @"E:\Gebruiker\Documents\TFS\Private Frameworks\HTM\01 Main\HTM.Ported.Tests\PytonEmbrace\Files\HsWorker.py";
            string result = PythonConverter.ConvertString(File.ReadAllText(fileToConvert));

            Console.WriteLine(result);
            
        }
    }
}