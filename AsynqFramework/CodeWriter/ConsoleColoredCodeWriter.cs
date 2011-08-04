using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace AsynqFramework.CodeWriter
{
    public class ConsoleColoredCodeWriter : CodeWriterBase
    {
        public override void Format(TextWriter tw, string indentString, int indentationLevel, string newLine)
        {
            bool writingToConsole = (tw == Console.Out);
            
            // TODO: WTF did I put this in here for?
            //Reset();

            foreach (var tok in output)
            {
                switch (tok.TokenType)
                {
                    case TokenType.Newline:
                        tw.WriteLine();
                        tw.Write(String.Concat(Enumerable.Repeat<string>(indentString, tok.IndentationDepth.Value).ToArray()));
                        break;
                    case TokenType.Comment:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.Green;
                        tw.Write(tok.Text);
                        break;
                    case TokenType.Keyword:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.DarkCyan;
                        tw.Write(tok.Text);
                        break;
                    case TokenType.ValueType:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.Yellow;
                        tw.Write(tok.Text);
                        break;
                    case TokenType.ClassType:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.Magenta;
                        tw.Write(tok.Text);
                        break;
                    case TokenType.InterfaceType:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.DarkYellow;
                        tw.Write(tok.Text);
                        break;
                    case TokenType.Identifier:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.Gray;
                        tw.Write(tok.Text);
                        break;
                    case TokenType.ConstantString:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.Red;
                        tw.Write(tok.Text);
                        break;
                    case TokenType.ConstantIntegral:
                    case TokenType.Unformatted:
                    default:
                        if (writingToConsole) Console.ForegroundColor = ConsoleColor.Gray;
                        tw.Write(tok.Text);
                        break;
                }
            }
        }
    }
}
