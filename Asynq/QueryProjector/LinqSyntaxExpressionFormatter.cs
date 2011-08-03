// C# LINQ syntax Expression formatter.
// (c) 2010 James S. Dunne
//
// I am calling this "Good Enough" for now. It is intended only for display purposes, not for creating compilable
// C# syntax.
//
// Known caveats:
// * `select` clauses may be missing in certain circumstances.
// * `from` clauses may be missing in certain circumstances.
// * `select` clauses may be followed by `where` clauses which is invalid in LINQ syntax.
// * `orderby` clauses may be followed by `select` clauses which is invalid in LINQ syntax.
// * Most constant value types are not supported for display purposes.
// * group X by Y into Z clause is not fully supported.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.IO;
using AsynqFramework.CodeWriter;

namespace AsynqFramework.QueryProjector
{
    /// <summary>
    /// A lambda expression formatter which attempts to format the expression tree back into
    /// C# LINQ syntax.
    /// </summary>
    public class LinqSyntaxExpressionFormatter : ExpressionVisitor
    {
        protected readonly CodeWriterBase _writer;

        public LinqSyntaxExpressionFormatter(CodeWriterBase writer)
        {
            this._writer = writer;
        }

        protected void Reset()
        {
            _writer.Reset();
            state = new Stack<VisitorState>();
            state.Push(new VisitorState());
        }

        protected virtual Expression VisitQuery(Expression query)
        {
            return this.Visit(query);
        }

        /// <summary>
        /// Formats a LINQ query expression into C# LINQ syntax.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public virtual string Format(Expression query)
        {
            Reset();

            VisitQuery(query);

            return _writer.ToString();
        }

        /// <summary>
        /// Writes the LINQ query to the TextWriter.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="tw"></param>
        public virtual void WriteFormat(Expression query, TextWriter tw)
        {
            Reset();

            VisitQuery(query);

            _writer.Format(tw, null, 0, null);
        }

        /// <summary>
        /// Temporary recursion state.
        /// </summary>
        protected class VisitorState
        {
            public bool displayLambdaParameters = true;
            public bool wroteSelect = false;
            public bool topLevel = true;

            public VisitorState()
            {
            }

            public VisitorState(VisitorState ns)
            {
                this.displayLambdaParameters = ns.displayLambdaParameters;
                this.wroteSelect = ns.wroteSelect;
                this.topLevel = ns.topLevel;
            }
        }

        protected Stack<VisitorState> state;
        /// <summary>
        /// Get the current state.
        /// </summary>
        /// <returns></returns>
        protected VisitorState GetState()
        {
            return state.Peek();
        }
        /// <summary>
        /// Restore the last state.
        /// </summary>
        /// <returns></returns>
        protected VisitorState RestoreState()
        {
            return state.Pop();
        }
        /// <summary>
        /// Save this current state. Can be recovered with RestoreState().
        /// </summary>
        /// <param name="st"></param>
        /// <returns></returns>
        protected VisitorState PushState(params Action<VisitorState>[] st)
        {
            VisitorState ns = new VisitorState(GetState());
            foreach (var act in st) act(ns);
            state.Push(ns);
            return ns;
        }
        /// <summary>
        /// Update this current state.
        /// </summary>
        /// <param name="st"></param>
        /// <returns></returns>
        protected VisitorState UpdateState(params Action<VisitorState>[] st)
        {
            VisitorState ns = GetState();
            foreach (var act in st) act(ns);
            return ns;
        }

        #region Visitor methods

        private Expression VisitSource(Expression e, Expression fromVar)
        {
            if (shouldWriteFrom(e))
            {
                _writer.WriteKeyword("from");
                _writer.WriteWhitespace(" ");
                this.Visit(fromVar);
                _writer.WriteWhitespace(" ");
                _writer.WriteKeyword("in");
                _writer.WriteWhitespace(" ");
                this.Visit(e);
                return e;
            }
            else
            {
                return this.Visit(e);
            }
        }

        private bool shouldWriteFrom(Expression e)
        {
            if (e.NodeType == ExpressionType.Call)
            {
                // Write the 'from' clause if the method call is not a Queryable or Enumerable call:
                MethodCallExpression m = (MethodCallExpression)e;
                return ((m.Method.DeclaringType != typeof(Queryable)) && (m.Method.DeclaringType != typeof(Enumerable)));
            }
            else if (e.NodeType == ExpressionType.Constant)
                return true;
            return false;
        }

        private void WriteExpressionListCommaDelimited(ReadOnlyCollection<Expression> args)
        {
            for (int i = 0; i < args.Count; ++i)
            {
                this.Visit(args[i]);
                if (i < args.Count - 1)
                {
                    _writer.WriteOperator(",");
                    _writer.WriteWhitespace(" ");
                }
            }
        }

        private Expression VisitSubquery(Expression e)
        {
            bool nontrivial = (e.NodeType != ExpressionType.Constant);
            if (nontrivial)
            {
                _writer.WriteOperator("(");
                _writer.Indent();
                _writer.WriteNewline();
            }
            PushState(x => x.wroteSelect = false, x => x.topLevel = true);
            this.Visit(e);
            if (nontrivial)
            {
                _writer.Unindent();
                _writer.WriteNewline();
                _writer.WriteOperator(")");
            }
            RestoreState();
            return e;
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if ((m.Method.DeclaringType != typeof(Queryable)) && (m.Method.DeclaringType != typeof(Enumerable)))
            {
                if (m.Object != null)
                {
                    this.Visit(m.Object);
                    if (m.Object.Type.IsArray && (m.Method.Name == "get_Item"))
                    {
                        _writer.WriteOperator("[");
                        WriteExpressionListCommaDelimited(m.Arguments);
                        _writer.WriteOperator("]");
                    }
                    else if (m.Object.Type.IsGenericType && (m.Object.Type.Name == "List`1") && (m.Method.Name == "get_Item"))
                    {
                        _writer.WriteOperator("[");
                        WriteExpressionListCommaDelimited(m.Arguments);
                        _writer.WriteOperator("]");
                    }
                    else
                    {
                        _writer.WriteOperator(".");
                        _writer.WriteIdentifier(m.Method.Name);
                        _writer.WriteOperator("(");
                        WriteExpressionListCommaDelimited(m.Arguments);
                        _writer.WriteOperator(")");
                    }
                }
                else
                {
                    Debug.Assert(m.Object == null);
                    // Static method call?
                    _writer.WriteIdentifier(m.Method.DeclaringType.FullName);
                    _writer.WriteOperator(".");
                    _writer.WriteIdentifier(m.Method.Name);
                    _writer.WriteOperator("(");
                    WriteExpressionListCommaDelimited(m.Arguments);
                    _writer.WriteOperator(")");
                }
                return m;
            }

            NewExpression ne;
            LambdaExpression le;
            Expression fromVar, joinVar;
            VisitorState tmp;
            List<CodeWriterBase.OutputToken> prevOut, tmpOut;
            bool isTopLevel = true;

            switch (m.Method.Name)
            {
                case "Select":
                    fromVar = DiscoverFromParameter(m.Arguments[1]);
                    isTopLevel = GetState().topLevel;
                    UpdateState(x => x.topLevel = false);

                    this.VisitSource(m.Arguments[0], fromVar);

                    if ((!isTopLevel) && ((ne = GetQuotedLambdaNew(m.Arguments[1])) != null) && (ne.Members.Count == 2))
                    {
                        // TODO: test all cases!
                        _writer.WriteNewline();
                        _writer.WriteKeyword("let");
                        _writer.WriteWhitespace(" ");
                        _writer.WriteIdentifier(ne.Members[1].Name.RemoveIfStartsWith("get_"));
                        _writer.WriteWhitespace(" ");
                        _writer.WriteOperator("=");
                        _writer.WriteWhitespace(" ");
                        this.Visit(ne.Arguments[1]);
                    }
                    else
                    {
                        _writer.WriteNewline();
                        _writer.WriteKeyword("select");
                        _writer.WriteWhitespace(" ");
                        PushState(x => x.displayLambdaParameters = false);
                        this.Visit(m.Arguments[1]);
                        RestoreState();
                        UpdateState(x => x.wroteSelect = true);
                    }

                    break;
                case "SelectMany":
                    fromVar = DiscoverFromParameter(m.Arguments[1]);
                    isTopLevel = GetState().topLevel;
                    UpdateState(x => x.topLevel = false);

                    this.VisitSource(m.Arguments[0], fromVar);

                    _writer.WriteWhitespace(" ");
                    // or _writer.WriteNewline();
                    _writer.WriteKeyword("from");
                    _writer.WriteWhitespace(" ");
                    this.Visit(DiscoverJoinFromParameter(m.Arguments[2]));
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("in");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(m.Arguments[1]);
                    RestoreState();

                    le = GetQuotedLambda(m.Arguments[2]);
                    if (le != null)
                    {
                        if (le.Body.NodeType != ExpressionType.New)
                        {
                            _writer.WriteNewline();
                            _writer.WriteKeyword("select");
                            _writer.WriteWhitespace(" ");
                            this.Visit(le.Body);
                            UpdateState(x => x.wroteSelect = true);
                        }
                        else if (!(ne = (NewExpression)le.Body).Members[0].Name.StartsWith("get_<>h__TransparentIdentifier"))
                        {
                            _writer.WriteNewline();
                            _writer.WriteKeyword("select");
                            _writer.WriteWhitespace(" ");
                            PushState(x => x.displayLambdaParameters = false);
                            this.VisitNew(ne);
                            RestoreState();
                            UpdateState(x => x.wroteSelect = true);
                        }
                    }
                    break;
                case "Where":
                    fromVar = DiscoverFromParameter(m.Arguments[1]);
                    isTopLevel = GetState().topLevel;
                    UpdateState(x => x.topLevel = false);

                    // Create a temporary output token list:
                    prevOut = _writer.OutputList;
                    tmpOut = new List<CodeWriterBase.OutputToken>();

                    _writer.OutputList = tmpOut;
                    this.VisitSource(m.Arguments[0], fromVar);
                    _writer.OutputList = prevOut;

                    tmp = GetState();
                    // If we wrote a select clause for the source query, then we need to
                    // wrap the source query in a from clause:
                    if (tmp.wroteSelect)
                    {
                        // Surround the source query in `from x in (...)`:
                        _writer.WriteKeyword("from");
                        _writer.WriteWhitespace(" ");
                        this.Visit(fromVar);
                        _writer.WriteWhitespace(" ");
                        _writer.WriteKeyword("in");
                        _writer.WriteWhitespace(" ");
                        _writer.WriteOperator("(");
                        _writer.Indent();
                        _writer.WriteNewline();

                        // Go back and adjust the indentation level for those tokens:
                        for (int i = 0; i < tmpOut.Count; ++i)
                        {
                            if (!tmpOut[i].IndentationDepth.HasValue) continue;
                            ++tmpOut[i].IndentationDepth;
                        }
                        // Write out the newly indented code:
                        _writer.OutputList.AddRange(tmpOut);

                        _writer.Unindent();
                        _writer.WriteNewline();
                        _writer.WriteOperator(")");
                        // Clear the flag that indicates that this query has written a `select` clause because
                        // we've effectively created a new query.
                        tmp.wroteSelect = false;
                    }
                    else
                    {
                        // Write out the source as normal:
                        _writer.OutputList.AddRange(tmpOut);
                    }

                    _writer.WriteNewline();
                    _writer.WriteKeyword("where");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(m.Arguments[1]);
                    RestoreState();

                    if (isTopLevel && !tmp.wroteSelect)
                    {
                        _writer.WriteNewline();
                        _writer.WriteKeyword("select");
                        _writer.WriteWhitespace(" ");
                        this.Visit(fromVar);
                        UpdateState(x => x.wroteSelect = true);
                    }
                    break;
                case "Join":
                    fromVar = DiscoverFromParameter(m.Arguments[2]);
                    isTopLevel = GetState().topLevel;
                    UpdateState(x => x.topLevel = false);

                    this.VisitSource(m.Arguments[0], fromVar);

                    tmp = GetState();

                    _writer.WriteNewline();
                    _writer.WriteKeyword("join");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    joinVar = DiscoverJoinFromParameter(m.Arguments[4]);
                    this.Visit(joinVar);
                    RestoreState();
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("in");
                    _writer.WriteWhitespace(" ");
                    this.VisitSubquery(m.Arguments[1]);
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("on");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(m.Arguments[2]);
                    RestoreState();
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("equals");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(m.Arguments[3]);
                    RestoreState();

                    if (isTopLevel && !tmp.wroteSelect)
                    {
                        _writer.WriteNewline();
                        _writer.WriteKeyword("select");
                        _writer.WriteWhitespace(" ");
                        // TODO: confirm no shortcuts required.
                        le = GetQuotedLambda(m.Arguments[4]);
                        this.Visit(le != null ? le.Body : joinVar);
                        UpdateState(x => x.wroteSelect = true);
                    }
                    break;
                case "GroupJoin":
                    fromVar = DiscoverFromParameter(m.Arguments[2]);
                    isTopLevel = GetState().topLevel;
                    UpdateState(x => x.topLevel = false);

                    this.VisitSource(m.Arguments[0], fromVar);

                    _writer.WriteNewline();
                    _writer.WriteKeyword("join");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(DiscoverFromParameter(m.Arguments[3]));
                    RestoreState();
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("in");
                    _writer.WriteWhitespace(" ");
                    this.VisitSubquery(m.Arguments[1]);
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("on");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(m.Arguments[2]);
                    RestoreState();
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("equals");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(m.Arguments[3]);
                    RestoreState();
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("into");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(DiscoverJoinFromParameter(m.Arguments[4]));
                    RestoreState();

                    if (isTopLevel && !GetState().wroteSelect)
                    {
                        _writer.WriteNewline();
                        _writer.WriteKeyword("select");
                        _writer.WriteWhitespace(" ");
                        this.Visit(fromVar);
                        UpdateState(x => x.wroteSelect = true);
                    }
                    break;
                case "GroupBy":
                    this.Visit(m.Arguments[0]);
                    _writer.WriteNewline();
                    _writer.WriteKeyword("group");
                    _writer.WriteWhitespace(" ");
                    le = GetQuotedLambda(m.Arguments[2]);
                    if (le != null)
                    {
                        this.Visit(le.Body);
                    }
                    else
                    {
                        _writer.WriteComment("/* fail */");
                    }
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("by");
                    _writer.WriteWhitespace(" ");
                    PushState(x => x.displayLambdaParameters = false);
                    this.Visit(m.Arguments[1]);
                    RestoreState();
                    // TODO: This has to be generated on the parent select clause...
                    //_writer.WriteWhitespace(" ");
                    //_writer.WriteKeyword("into");
                    //_writer.WriteWhitespace(" ");
                    //this.Visit(m.Arguments[1]);
                    break;
                case "Take":
                case "Skip":
                    this.Visit(m.Arguments[0]);
                    _writer.WriteNewline();
                    _writer.WriteOperator(".");
                    _writer.WriteIdentifier(m.Method.Name);
                    _writer.WriteOperator("(");
                    for (int i = 1; i < m.Arguments.Count; ++i)
                    {
                        this.Visit(m.Arguments[i]);
                        if (i < m.Arguments.Count - 1)
                        {
                            _writer.WriteOperator(",");
                            _writer.WriteWhitespace(" ");
                        }
                    }
                    _writer.WriteOperator(")");
                    break;
                case "OrderBy":
                case "OrderByDescending":
                    this.VisitSource(m.Arguments[0], DiscoverFromParameter(m.Arguments[1]));
                    if (GetState().wroteSelect)
                    {
                        // LINQ extension method syntax:
                        _writer.WriteNewline();
                        _writer.WriteOperator(".");
                        _writer.WriteIdentifier(m.Method.Name);
                        _writer.WriteOperator("(");
                        for (int i = 1; i < m.Arguments.Count; ++i)
                        {
                            this.Visit(m.Arguments[i]);
                            if (i < m.Arguments.Count - 1)
                            {
                                _writer.WriteOperator(",");
                                _writer.WriteWhitespace(" ");
                            }
                        }
                        _writer.WriteOperator(")");
                    }
                    else
                    {
                        // LINQ syntax inside a LINQ statement:
                        _writer.WriteNewline();
                        _writer.WriteKeyword("orderby");
                        _writer.WriteWhitespace(" ");
                        PushState(x => x.displayLambdaParameters = false);
                        this.Visit(m.Arguments[1]);
                        RestoreState();
                        _writer.WriteWhitespace(" ");
                        if (m.Method.Name.EndsWith("Descending"))
                            _writer.WriteKeyword("descending");
                        else
                            _writer.WriteKeyword("ascending");
                    }
                    break;
                case "ThenBy":
                case "ThenByDescending":
                    this.Visit(m.Arguments[0]);
                    if (GetState().wroteSelect)
                    {
                        // LINQ extension method syntax:
                        _writer.WriteNewline();
                        _writer.WriteOperator(".");
                        _writer.WriteIdentifier(m.Method.Name);
                        _writer.WriteOperator("(");
                        for (int i = 1; i < m.Arguments.Count; ++i)
                        {
                            this.Visit(m.Arguments[i]);
                            if (i < m.Arguments.Count - 1)
                            {
                                _writer.WriteOperator(",");
                                _writer.WriteWhitespace(" ");
                            }
                        }
                        _writer.WriteOperator(")");
                    }
                    else
                    {
                        _writer.WriteOperator(",");
                        _writer.WriteNewline();
                        _writer.WriteWhitespace("        ");
                        PushState(x => x.displayLambdaParameters = false);
                        this.Visit(m.Arguments[1]);
                        RestoreState();
                        _writer.WriteWhitespace(" ");
                        if (m.Method.Name.EndsWith("Descending"))
                            _writer.WriteKeyword("descending");
                        else
                            _writer.WriteKeyword("ascending");
                    }
                    break;
                case "Union":
                case "SkipWhile":
                case "TakeWhile":
                case "All":
                case "Concat":
                case "Intersect":
                case "Except":
                case "ElementAt":
                case "ElementAtOrDefault":
                case "Last":
                case "LastOrDefault":
                case "Reverse":
                case "SequenceEqual":
                case "Count":
                case "Sum":
                case "Min":
                case "Max":
                case "Average":
                case "Contains":
                case "Distinct":
                case "DefaultIfEmpty":
                    bool nontrivial = (m.Arguments[0].NodeType != ExpressionType.MemberAccess) &&
                        (m.Arguments[0].NodeType != ExpressionType.Parameter);
                    if (nontrivial)
                    {
                        _writer.WriteOperator("(");
                        _writer.Indent();
                        _writer.WriteNewline();
                    }
                    this.Visit(m.Arguments[0]);
                    if (nontrivial)
                    {
                        _writer.Unindent();
                        _writer.WriteNewline();
                        _writer.WriteOperator(")");
                    }
                    _writer.WriteOperator(".");
                    _writer.WriteIdentifier(m.Method.Name);
                    _writer.WriteOperator("(");
                    for (int i = 1; i < m.Arguments.Count; ++i)
                    {
                        this.Visit(m.Arguments[i]);
                        if (i < m.Arguments.Count - 1)
                        {
                            _writer.WriteOperator(",");
                            _writer.WriteWhitespace(" ");
                        }
                    }
                    _writer.WriteOperator(")");
                    break;
                default:
                    _writer.WriteNewline();

                    // TODO: Support more Queryable methods.
                    _writer.WriteComment("/* " + m.Method.Name + " */");
                    break;
            }

            return m;
        }

        private LambdaExpression GetQuotedLambda(Expression e)
        {
            if (e.NodeType != ExpressionType.Quote) return null;
            Expression ueOp = ((UnaryExpression)e).Operand;
            if (ueOp.NodeType != ExpressionType.Lambda) return null;
            LambdaExpression le = (LambdaExpression)ueOp;
            return le;
        }

        private NewExpression GetQuotedLambdaNew(Expression e)
        {
            LambdaExpression le = GetQuotedLambda(e);
            if (le == null) return null;
            if (le.Body.NodeType != ExpressionType.New) return null;
            NewExpression ne = (NewExpression)le.Body;
            return ne;
        }

        private Expression DiscoverJoinFromParameter(Expression e)
        {
            LambdaExpression le = GetQuotedLambda(e);
            if (le == null) return null;
            if (le.Parameters.Count < 2) return e;
            return le.Parameters[1];
        }

        private Expression DiscoverFromParameter(Expression e)
        {
            LambdaExpression le = GetQuotedLambda(e);
            if (le == null) return null;
            if (le.Parameters.Count < 1) return e;
            return le.Parameters[0];
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c.Value == null)
            {
                _writer.WriteKeyword("null");
                return c;
            }

            Type vType = c.Type;
            Type cValueType = c.Value.GetType();
            if (vType.IsGenericType && (vType.GetGenericTypeDefinition() == typeof(System.Data.Linq.Table<>)))
            {
                // for a Table<T>, just display T:
                _writer.WriteType(vType.GetGenericArguments()[0]);
            }
#if false
            // TODO: how to discover this case...
            else if (cValueType.FullName == "System.Linq.Enumerable+<RangeIterator>d__b1")
            {
                _writer.WriteType(cValueType.DeclaringType);
                _writer.WriteOperator(".");
                _writer.WriteIdentifier("Range");
                _writer.WriteOperator("(");
                System.Reflection.FieldInfo fiStart = cValueType.GetField("start");
                System.Reflection.FieldInfo fiCount = cValueType.GetField("count");
                _writer.WriteValue(fiStart.FieldType, fiStart.GetValue(c.Value));
                _writer.WriteOperator(",");
                _writer.WriteWhitespace(" ");
                _writer.WriteValue(fiCount.FieldType, fiCount.GetValue(c.Value));
                _writer.WriteOperator(")");
            }
#endif
            else
            {
                _writer.WriteValue(vType, c.Value);
            }

            return c;
        }

        private Expression RemoveTransparentIdentifiers(MemberExpression m)
        {
            Expression newInner = m.Expression;

            ParameterExpression p;
            ConstantExpression c;
            MemberExpression me;

            if ((p = newInner as ParameterExpression) != null)
            {
                if (p.Name.StartsWith("<>h__TransparentIdentifier"))
                {
                    return Expression.Parameter(m.Type, m.Member.Name);
                }
            }
            else if ((c = newInner as ConstantExpression) != null)
            {
                if (c.Type.Name.StartsWith("<>h__TransparentIdentifier"))
                {
                    return Expression.Parameter(m.Type, m.Member.Name);
                }
                else if (c.Type.Name.StartsWith("<>c__DisplayClass"))
                {
                    return Expression.Parameter(m.Type, m.Member.Name);
                }
            }
            else if ((me = newInner as MemberExpression) != null)
            {
                if (me.Member.Name.StartsWith("<>h__TransparentIdentifier"))
                {
                    System.Reflection.PropertyInfo pInfo = (System.Reflection.PropertyInfo)m.Member;
                    return Expression.Parameter(pInfo.PropertyType, pInfo.Name);
                }
                else
                {
                    Expression inner = RemoveTransparentIdentifiers(me);
                    if (inner != me)
                    {
                        return Expression.MakeMemberAccess(inner, m.Member);
                    }
                    return m;
                }
            }

            return m;
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            Expression e = RemoveTransparentIdentifiers(m);
            MemberExpression me = e as MemberExpression;
            if (me != null)
            {
                if (me.Expression == null)
                {
                    // Static property reference:
                    _writer.WriteType(me.Member.DeclaringType);
                }
                else
                {
                    this.Visit(me.Expression);
                }
                _writer.WriteOperator(".");
                _writer.WriteIdentifier(me.Member.Name);
            }
            else
            {
                this.Visit(e);
            }
            return e;
        }

        protected override NewExpression VisitNew(NewExpression nex)
        {
            _writer.WriteKeyword("new");
            _writer.WriteWhitespace(" ");
            if (nex.Type.Name.StartsWith("<>f__AnonymousType"))
            {
                _writer.WriteOperator("{");
                _writer.WriteWhitespace(" ");
                if ((nex.Members != null) && (nex.Members.Count == nex.Arguments.Count))
                {
                    for (int i = 0; i < nex.Arguments.Count; ++i)
                    {
                        _writer.WriteIdentifier(nex.Members[i].Name.RemoveIfStartsWith("get_"));

                        List<CodeWriterBase.OutputToken> prevOut = _writer.OutputList;
                        List<CodeWriterBase.OutputToken> tmpOut = new List<CodeWriterBase.OutputToken>();

                        // Write out the initializer expression to a temporary list:
                        _writer.OutputList = tmpOut;
                        this.Visit(nex.Arguments[i]);
                        _writer.OutputList = prevOut;

                        // See if we can use the shorthand syntax of just 'a' instead of 'a = a':
                        if (!((tmpOut.Count == 1) &&
                            (tmpOut[0].TokenType == CodeWriterBase.TokenType.Identifier) &&
                            (tmpOut[0].Text == nex.Members[i].Name.RemoveIfStartsWith("get_"))))
                        {
                            _writer.WriteWhitespace(" ");
                            _writer.WriteOperator("=");
                            _writer.WriteWhitespace(" ");
                            _writer.OutputList.AddRange(tmpOut);
                        }

                        if (i < nex.Arguments.Count - 1)
                        {
                            _writer.WriteOperator(",");
                            _writer.WriteWhitespace(" ");
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < nex.Arguments.Count; ++i)
                    {
                        this.Visit(nex.Arguments[i]);
                        if (i < nex.Arguments.Count - 1)
                        {
                            _writer.WriteOperator(",");
                            _writer.WriteWhitespace(" ");
                        }
                    }
                }
                _writer.WriteWhitespace(" ");
                _writer.WriteOperator("}");
            }
            else
            {
                _writer.WriteType(nex.Type);
                _writer.WriteOperator("(");
                for (int i = 0; i < nex.Arguments.Count; ++i)
                {
                    this.Visit(nex.Arguments[i]);
                    if (i < nex.Arguments.Count - 1)
                    {
                        _writer.WriteOperator(",");
                        _writer.WriteWhitespace(" ");
                    }
                }
                _writer.WriteOperator(")");
            }
            return nex;
        }

        protected override Expression VisitLambda(LambdaExpression lambda)
        {
            if (GetState().displayLambdaParameters)
            {
                if (lambda.Parameters.Count > 1)
                {
                    _writer.WriteOperator("(");
                    for (int i = 0; i < lambda.Parameters.Count; ++i)
                    {
                        this.Visit(lambda.Parameters[i]);
                        if (i < lambda.Parameters.Count - 1)
                        {
                            _writer.WriteOperator(",");
                            _writer.WriteWhitespace(" ");
                        }
                    }
                    _writer.WriteOperator(")");
                }
                else if (lambda.Parameters.Count == 1)
                {
                    this.Visit(lambda.Parameters[0]);
                }
                else
                {
                    _writer.WriteOperator("(");
                    _writer.WriteOperator(")");
                }
                _writer.WriteWhitespace(" ");
                _writer.WriteOperator("=>");
                _writer.WriteWhitespace(" ");
            }
            this.Visit(lambda.Body);
            return lambda;
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            _writer.WriteIdentifier(p.Name);
            return p;
        }

        protected override Expression VisitInvocation(InvocationExpression iv)
        {
            this.Visit(iv.Expression);
            _writer.WriteOperator("(");
            WriteExpressionListCommaDelimited(iv.Arguments);
            _writer.WriteOperator(")");
            return iv;
        }

        protected override Expression VisitConditional(ConditionalExpression c)
        {
            this.Visit(c.Test);
            _writer.WriteWhitespace(" ");
            _writer.WriteOperator("?");
            _writer.WriteWhitespace(" ");
            this.Visit(c.IfTrue);
            _writer.WriteWhitespace(" ");
            _writer.WriteOperator(":");
            _writer.WriteWhitespace(" ");
            this.Visit(c.IfFalse);
            return c;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            if (b.NodeType == ExpressionType.ArrayIndex)
            {
                this.Visit(b.Left);
                _writer.WriteOperator("[");
                this.Visit(b.Right);
                _writer.WriteOperator("]");
                return b;
            }

            _writer.WriteOperator("(");
            this.Visit(b.Left);
            _writer.WriteWhitespace(" ");
            switch (b.NodeType)
            {
                case ExpressionType.Add:
                    _writer.WriteWhitespace("+"); break;
                case ExpressionType.AddChecked:
                    _writer.WriteWhitespace("+"); break;
                case ExpressionType.Subtract:
                    _writer.WriteWhitespace("-"); break;
                case ExpressionType.SubtractChecked:
                    _writer.WriteWhitespace("-"); break;
                case ExpressionType.Multiply:
                    _writer.WriteWhitespace("*"); break;
                case ExpressionType.MultiplyChecked:
                    _writer.WriteWhitespace("*"); break;
                case ExpressionType.Divide:
                    _writer.WriteWhitespace("/"); break;
                case ExpressionType.Modulo:
                    _writer.WriteWhitespace("%"); break;
                case ExpressionType.And:
                    _writer.WriteWhitespace("&"); break;
                case ExpressionType.AndAlso:
                    _writer.WriteWhitespace("&&"); break;
                case ExpressionType.Or:
                    _writer.WriteWhitespace("|"); break;
                case ExpressionType.OrElse:
                    _writer.WriteWhitespace("||"); break;
                case ExpressionType.LessThan:
                    _writer.WriteWhitespace("<"); break;
                case ExpressionType.LessThanOrEqual:
                    _writer.WriteWhitespace("<="); break;
                case ExpressionType.GreaterThan:
                    _writer.WriteWhitespace(">"); break;
                case ExpressionType.GreaterThanOrEqual:
                    _writer.WriteWhitespace(">="); break;
                case ExpressionType.Equal:
                    _writer.WriteWhitespace("=="); break;
                case ExpressionType.NotEqual:
                    _writer.WriteWhitespace("!="); break;
                case ExpressionType.Coalesce:
                    _writer.WriteWhitespace("??"); break;
                case ExpressionType.ArrayIndex:
                    _writer.WriteWhitespace("[]"); break;
                case ExpressionType.RightShift:
                    _writer.WriteWhitespace(">>"); break;
                case ExpressionType.LeftShift:
                    _writer.WriteWhitespace("<<"); break;
                case ExpressionType.ExclusiveOr:
                    _writer.WriteWhitespace("^"); break;

                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
            }
            _writer.WriteWhitespace(" ");
            this.Visit(b.Right);
            _writer.WriteOperator(")");
            return b;
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Negate:
                    _writer.WriteOperator("-");
                    this.Visit(u.Operand);
                    break;
                case ExpressionType.NegateChecked:
                    _writer.WriteOperator("-");
                    this.Visit(u.Operand);
                    break;
                case ExpressionType.Not:
                    _writer.WriteOperator("!");
                    this.Visit(u.Operand);
                    break;
                case ExpressionType.Convert:
                    _writer.WriteOperator("(");
                    _writer.WriteType(u.Type);
                    _writer.WriteOperator(")");
                    this.Visit(u.Operand);
                    break;
                case ExpressionType.ConvertChecked:
                    _writer.WriteKeyword("checked");
                    _writer.WriteOperator("(");
                    _writer.WriteOperator("(");
                    _writer.WriteType(u.Type);
                    _writer.WriteOperator(")");
                    this.Visit(u.Operand);
                    _writer.WriteOperator(")");
                    break;
                case ExpressionType.ArrayLength:
                    this.Visit(u.Operand);
                    // TODO: confirm!
                    _writer.WriteOperator(".");
                    _writer.WriteIdentifier("Length");
                    break;
                case ExpressionType.Quote:
                    this.Visit(u.Operand);
                    break;
                case ExpressionType.TypeAs:
                    _writer.WriteOperator("(");
                    this.Visit(u.Operand);
                    // TODO: confirm!
                    _writer.WriteWhitespace(" ");
                    _writer.WriteKeyword("as");
                    _writer.WriteWhitespace(" ");
                    _writer.WriteType(u.Type);
                    _writer.WriteOperator(")");
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
            }
            return u;
        }

        protected override Expression VisitNewArray(NewArrayExpression na)
        {
            _writer.WriteKeyword("new");
            _writer.WriteWhitespace(" ");
            Debug.Assert(na.Type.IsArray);
            _writer.WriteType(na.Type.GetElementType());
            _writer.WriteOperator("[");
            if (na.NodeType == ExpressionType.NewArrayBounds)
            {
                WriteExpressionListCommaDelimited(na.Expressions);
            }
            _writer.WriteOperator("]");
            if (na.NodeType == ExpressionType.NewArrayInit)
            {
                _writer.WriteWhitespace(" ");
                _writer.WriteOperator("{");
                _writer.WriteWhitespace(" ");
                WriteExpressionListCommaDelimited(na.Expressions);
                _writer.WriteWhitespace(" ");
                _writer.WriteOperator("}");
            }
            return na;
        }

        protected override Expression VisitTypeIs(TypeBinaryExpression b)
        {
            this.Visit(b.Expression);
            _writer.WriteWhitespace(" ");
            _writer.WriteKeyword("is");
            _writer.WriteWhitespace(" ");
            _writer.WriteType(b.TypeOperand);
            return b;
        }

        protected override Expression VisitListInit(ListInitExpression init)
        {
            this.VisitNew(init.NewExpression);
            _writer.WriteWhitespace(" ");
            _writer.WriteOperator("{");
            _writer.WriteWhitespace(" ");
            WriteExpressionListCommaDelimited(new ReadOnlyCollection<Expression>((
                from ine in init.Initializers
                select ine.Arguments[0]
            ).ToList()));
            _writer.WriteWhitespace(" ");
            _writer.WriteOperator("}");
            return init;
        }

        protected override Expression VisitMemberInit(MemberInitExpression init)
        {
            NewExpression n = this.VisitNew(init.NewExpression);
            _writer.WriteWhitespace(" ");
            _writer.WriteOperator("{");
            _writer.WriteWhitespace(" ");
            for (int i = 0; i < init.Bindings.Count; ++i)
            {
                var binding = init.Bindings[i];
                if (binding.BindingType == MemberBindingType.Assignment)
                {
                    var ma = ((MemberAssignment)binding);
                    _writer.WriteIdentifier(ma.Member.Name);
                    _writer.WriteWhitespace(" ");
                    _writer.WriteOperator("=");
                    _writer.WriteWhitespace(" ");
                    this.Visit(ma.Expression);
                }
                if (i < init.Bindings.Count - 1)
                {
                    _writer.WriteOperator(",");
                    _writer.WriteWhitespace(" ");
                }
            }
            _writer.WriteWhitespace(" ");
            _writer.WriteOperator("}");
            return init;
        }

        protected override MemberListBinding VisitMemberListBinding(MemberListBinding binding)
        {
            return base.VisitMemberListBinding(binding);
        }

        protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding)
        {
            return base.VisitMemberMemberBinding(binding);
        }

        #endregion
    }
}
