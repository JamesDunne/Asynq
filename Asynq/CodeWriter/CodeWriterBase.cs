using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace AsynqFramework.CodeWriter
{
    public class CodeWriterBase
    {
        public CodeWriterBase()
        {
            InitializeWriter();
        }

        protected CodeWriterBase(int indentLevel, List<OutputToken> tokens)
        {
            this.indentLevel = indentLevel;
            this.output = new List<OutputToken>(tokens);
        }

        public void Reset()
        {
            InitializeWriter();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            using (StringWriter sw = new StringWriter(sb))
            {
                Format(sw, null, 0, null);
                sw.Flush();
            }

            return sb.ToString();
        }

        public virtual string ToString(string indentString, int indentationLevel, string newLine)
        {
            StringBuilder sb = new StringBuilder();

            using (StringWriter sw = new StringWriter(sb))
            {
                Format(sw, indentString, indentationLevel, newLine);
                sw.Flush();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats the code as plain-text to a TextWriter with the specified indentation and new-line settings.
        /// </summary>
        /// <param name="tw">TextWriter to write the output to</param>
        /// <param name="indentationLevel">The level of indentation to start at, defaults to 0.</param>
        /// <param name="indentString">The indentation string to use per each level of indentation, defaults to 4 spaces.</param>
        /// <param name="newLine">The environment-specific new-line delimiter string to use, defaults to Environment.NewLine.</param>
        public virtual void Format(TextWriter tw, string indentString, int indentationLevel, string newLine)
        {
            if (indentationLevel < 0) indentationLevel = 0;
            if (indentString == null) indentString = new string(' ', 4);
            if (newLine == null) newLine = Environment.NewLine;

            foreach (var tok in output)
            {
                switch (tok.TokenType)
                {
                    case TokenType.Newline:
                        tw.Write(newLine + String.Concat(Enumerable.Repeat<string>(indentString, indentationLevel + tok.IndentationDepth.Value).ToArray()));
                        break;
                    default:
                        tw.Write(tok.Text);
                        break;
                }
            }
        }

        public virtual CodeWriterBase Clone()
        {
            return new CodeWriterBase(indentLevel, output);
        }

        #region Virtual writer methods

        public enum TokenType
        {
            Unformatted,
            Whitespace,
            Comment,
            Newline,
            Keyword,
            Identifier,
            Operator,
            ClassType,
            ValueType,
            InterfaceType,
            ConstantIntegral,
            ConstantString,
            ConstantDecimal,
            ConstantFloat,
            ConstantDouble,
        }

        [DebuggerDisplay("{Text}, {TokenType}, {IndentationDepth}")]
        public class OutputToken
        {
            public string Text { get; set; }
            public TokenType TokenType { get; set; }
            public int? IndentationDepth { get; set; }
            public object State { get; set; }

            public OutputToken(string text)
            {
                this.Text = text;
                this.TokenType = TokenType.Unformatted;
                this.IndentationDepth = null;
                this.State = null;
            }

            public OutputToken(string text, int? depth)
            {
                this.Text = text;
                this.TokenType = TokenType.Unformatted;
                this.IndentationDepth = depth;
                this.State = null;
            }

            public OutputToken(string text, TokenType type)
            {
                this.Text = text;
                this.TokenType = type;
                this.IndentationDepth = null;
                this.State = null;
            }

            public OutputToken(string text, TokenType type, int? depth)
            {
                this.Text = text;
                this.TokenType = type;
                this.IndentationDepth = depth;
                this.State = null;
            }
        }

        protected List<OutputToken> output;

        public List<OutputToken> OutputList
        {
            get { return output; }
            set { output = value; }
        }

        protected int indentLevel = 0;
        public int IndentationLevel
        {
            get { return indentLevel; }
            set { indentLevel = value; }
        }

        protected virtual void InitializeWriter()
        {
            output = new List<OutputToken>();
            indentLevel = 0;
        }

        public virtual OutputToken Output(OutputToken tok)
        {
            this.output.Add(tok);
            return tok;
        }

        public OutputToken Output(string text)
        {
            var tok = new OutputToken(text);
            Output(tok);
            return tok;
        }

        public OutputToken Output(string text, TokenType type)
        {
            var tok = new OutputToken(text, type);
            Output(tok);
            return tok;
        }

        public OutputToken Output(string text, TokenType type, object state)
        {
            var tok = new OutputToken(text, type) { State = state };
            Output(tok);
            return tok;
        }

        public OutputToken OutputWithDepth(string text, TokenType type, int depth)
        {
            var tok = new OutputToken(text, type, depth);
            Output(tok);
            return tok;
        }

        /// <summary>
        /// Writes whitespace characters to the output.
        /// </summary>
        /// <param name="ws"></param>
        public virtual void WriteWhitespace(string ws) { Output(ws, TokenType.Whitespace, (object)ws); }
        /// <summary>
        /// Increase the indentation level for the next line.
        /// </summary>
        public virtual void Indent() { ++indentLevel; }
        /// <summary>
        /// Decrease the indentation level for the next line.
        /// </summary>
        public virtual void Unindent() { --indentLevel; }
        /// <summary>
        /// Writes a new line to the output followed by the indentation string.
        /// </summary>
        public virtual void WriteNewline()
        {
            OutputWithDepth(Environment.NewLine, TokenType.Newline, indentLevel);
        }

        /// <summary>
        /// Writes a C# comment to the output.
        /// </summary>
        /// <param name="cm">Comment text, must be a // single line comment or be a /* block comment */ form.</param>
        public virtual void WriteComment(string cm) { Output(cm, TokenType.Comment, (object)cm); }
        /// <summary>
        /// Writes operator characters to the output, such as parentheses and other non-alphanumeric/non-whitespace characters.
        /// </summary>
        /// <param name="op"></param>
        public virtual void WriteOperator(string op) { Output(op, TokenType.Operator, (object)op); }
        /// <summary>
        /// Writes a C# keyword to the output.
        /// </summary>
        /// <param name="kw"></param>
        public virtual void WriteKeyword(string kw) { Output(kw, TokenType.Keyword, (object)kw); }
        /// <summary>
        /// Writes the name of a type to the output. It will be formatted as a C# primitive type name if applicable.
        /// </summary>
        /// <param name="ty"></param>
        public virtual void WriteType(Type ty)
        {
            if (ty.IsGenericType && (ty.GetGenericTypeDefinition() == typeof(Nullable<>)))
            {
                WriteType(ty.GetGenericArguments()[0]);
                WriteOperator("?");
            }
            else if (ty.IsGenericType)
            {
                // FIXME: generate a comma-delimited list of arguments
                Output(ty.Name.Remove(ty.Name.Length - 2), ty.IsValueType ? TokenType.ValueType : ty.IsInterface ? TokenType.InterfaceType : TokenType.ClassType, ty);
                WriteOperator("<");
                Type[] args = ty.GetGenericArguments();
                for (int i = 0; i < args.Length; ++i)
                {
                    WriteType(args[i]);
                    if (i < args.Length - 1)
                    {
                        WriteOperator(",");
                        WriteWhitespace(" ");
                    }
                }
                WriteOperator(">");
            }
            else if (ty == typeof(void))
            {
                WriteKeyword("void");
            }
            else if (ty == typeof(string))
            {
                WriteKeyword("string");
            }
            else if (ty.IsPrimitive)
            {
                if (ty == typeof(int)) WriteKeyword("int");
                else if (ty == typeof(bool)) WriteKeyword("bool");
                else if (ty == typeof(decimal)) WriteKeyword("decimal");
                else if (ty == typeof(char)) WriteKeyword("char");
                else if (ty == typeof(sbyte)) WriteKeyword("sbyte");
                else if (ty == typeof(byte)) WriteKeyword("byte");
                else if (ty == typeof(short)) WriteKeyword("short");
                else if (ty == typeof(ushort)) WriteKeyword("ushort");
                else if (ty == typeof(uint)) WriteKeyword("uint");
                else if (ty == typeof(long)) WriteKeyword("long");
                else if (ty == typeof(ulong)) WriteKeyword("ulong");
                else if (ty == typeof(double)) WriteKeyword("double");
                else if (ty == typeof(float)) WriteKeyword("float");
                else WriteComment(String.Format("/* UNKNOWN PRIMITIVE TYPE {0} */", ty.FullName));
            }
            // TODO: other CLR to C# conversions here...
            else
            {
                Output(ty.Name, ty.IsValueType ? TokenType.ValueType : ty.IsInterface ? TokenType.InterfaceType : TokenType.ClassType, ty);
            }
        }
        /// <summary>
        /// Writes a C# identifier to the output.
        /// </summary>
        /// <param name="id"></param>
        public virtual void WriteIdentifier(string id) { Output(id, TokenType.Identifier, (object)id); }

        private static string escapeCSharp(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\'", "\\\'").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        public virtual void WritePrimitive(char value) { Output(String.Concat("\'", escapeCSharp(value.ToString()), "\'"), TokenType.ConstantString, (object)value); }
        public virtual void WritePrimitive(sbyte value) { Output(value.ToString(), TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(byte value) { Output(value.ToString(), TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(short value) { Output(value.ToString(), TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(ushort value) { Output(value.ToString() + "U", TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(int value) { Output(value.ToString(), TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(uint value) { Output(value.ToString() + "U", TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(long value) { Output(value.ToString() + "L", TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(ulong value) { Output(value.ToString() + "UL", TokenType.ConstantIntegral, (object)value); }
        public virtual void WritePrimitive(double value) { Output(value.ToString() + "d", TokenType.ConstantDouble, (object)value); }
        public virtual void WritePrimitive(float value) { Output(value.ToString() + "f", TokenType.ConstantFloat, (object)value); }
        public virtual void WritePrimitive(decimal value) { Output(value.ToString() + "m", TokenType.ConstantDecimal, (object)value); }
        public virtual void WritePrimitive(string value)
        {
            if (value == null)
            {
                Output("null", TokenType.Keyword);
                return;
            }

            string strRep = String.Concat("\"", escapeCSharp(value), "\"");
            Output(strRep, TokenType.ConstantString, (object)value);
        }

        /// <summary>
        /// Writes a C# constant value to the output.
        /// </summary>
        /// <param name="vType"></param>
        /// <param name="value"></param>
        public virtual void WriteValue(Type vType, object value)
        {
            if (vType.IsPrimitive)
            {
                if (vType == typeof(int)) WritePrimitive((int)value);
                else if (vType == typeof(bool))
                {
                    if ((bool)value == true) WriteKeyword("true");
                    else WriteKeyword("false");
                }
                else if (vType == typeof(decimal)) WritePrimitive((decimal)value);
                else if (vType == typeof(char)) WritePrimitive((char)value);
                else if (vType == typeof(sbyte)) WritePrimitive((sbyte)value);
                else if (vType == typeof(byte)) WritePrimitive((byte)value);
                else if (vType == typeof(short)) WritePrimitive((short)value);
                else if (vType == typeof(ushort)) WritePrimitive((ushort)value);
                else if (vType == typeof(uint)) WritePrimitive((uint)value);
                else if (vType == typeof(long)) WritePrimitive((long)value);
                else if (vType == typeof(ulong)) WritePrimitive((ulong)value);
                else if (vType == typeof(double)) WritePrimitive((double)value);
                else if (vType == typeof(float)) WritePrimitive((float)value);
                else
                {
                    WriteComment(String.Format("/* Unknown primitive type {0} */", vType.FullName));
                }
            }
            else if (vType == typeof(string))
            {
                WritePrimitive((string)value);
            }
            else
            {
                // TODO: other constant types for display here!
                Output(value.ToString(), TokenType.Unformatted, (object)value);
            }
        }

        #endregion
    }
}
