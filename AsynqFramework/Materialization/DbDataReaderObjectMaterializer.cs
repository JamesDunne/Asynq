using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;

namespace AsynqFramework.Materialization
{
    public sealed class DbDataReaderObjectMaterializer : IObjectMaterializer<DbDataReader>
    {
        [DebuggerDisplay(@"{Name} <- {SourceOrdinal}")]
        private class MaterializationState
        {
            public Type Type { get; set; }
            public int? SourceOrdinal { get; set; }

            public int? NullTestOrdinal { get; set; }

            public List<MaterializationState> Children { get; private set; }
            public MaterializationState Parent { get; private set; }

            public MaterializationState(Type type, MaterializationState parent)
            {
                this.Type = type;
                this.Children = new List<MaterializationState>();
                this.Parent = parent;
                if (parent != null)
                    parent.Children.Add(this);
            }

            public virtual string Name { get { return this.Type.Name; } }
            public virtual string FullName { get { return this.Type.FullName; } }

            internal object Materialize(DbDataReader dr)
            {
                // If no children, we are just a primitive column value:
                if (Children.Count == 0)
                {
                    Debug.Assert(this.SourceOrdinal.HasValue);
                    return dr.GetValue(this.SourceOrdinal.Value);
                }

                // Determine if we are null or not:
                if (NullTestOrdinal.HasValue)
                {
                    if (dr.IsDBNull(NullTestOrdinal.Value))
                        return null;
                }

                int childCount = Children.Count;

                // Materialize the children:
                object[] reverseChildValues = new object[childCount];
                for (int i = 0; i < childCount; ++i)
                {
                    int reverseI = childCount - 1 - i;
                    
                    reverseChildValues[reverseI] = Children[i].Materialize(dr);
                }

                if (Children[0] is CtorParameterMaterializationState)
                {
                    // Use the ctor with the materialized parameter values:

                    // FIXME: what if additional properties are available to assign?
                    return Activator.CreateInstance(this.Type, reverseChildValues);
                }
                else
                {
                    // Use a default ctor to create this object and assigned property values:

                    // FIXME: what if no default ctor available? Use combo approach of parameters and properties after-the-fact?
                    object curr = Activator.CreateInstance(this.Type);

                    for (int i = 0; i < childCount; ++i)
                    {
                        int reverseI = childCount - 1 - i;
                        Debug.Assert(Children[i] is PropertyMaterializationState);

                        PropertyMaterializationState pstate = (PropertyMaterializationState)Children[i];
                        pstate.PropertyInfo.SetValue(curr, reverseChildValues[reverseI], null);
                    }

                    return curr;
                }
            }
        }

        private class CtorParameterMaterializationState : MaterializationState
        {
            public ParameterInfo ParameterInfo { get; set; }

            public CtorParameterMaterializationState(Type type, MaterializationState parent, ParameterInfo parameterInfo)
                : base(type, parent)
            {
                this.ParameterInfo = parameterInfo;
            }

            public override string Name { get { return this.ParameterInfo.Name; } }
            public override string FullName { get { return this.Parent.FullName + "!" + this.Name; } }
        }

        private class PropertyMaterializationState : MaterializationState
        {
            public PropertyInfo PropertyInfo { get; set; }

            public PropertyMaterializationState(Type type, MaterializationState parent, PropertyInfo propertyInfo)
                : base(type, parent)
            {
                this.PropertyInfo = propertyInfo;
            }

            public override string Name { get { return this.PropertyInfo.Name; } }
            public override string FullName { get { return this.Parent.FullName + "." + this.Name; } }
        }

        private class DbDataReaderObjectMaterializationMapping : IObjectMaterializationMapping<DbDataReader>
        {
            public DbDataReader DataSource { get; private set; }

            internal MaterializationState Root { get; set; }

            internal DbDataReaderObjectMaterializationMapping(DbDataReader dr, MaterializationState root)
            {
                this.DataSource = dr;
                this.Root = root;
            }
        }

        public IObjectMaterializationMapping<DbDataReader> BuildMaterializationMapping(Type destinationType, DbDataReader dataSource)
        {
            if (destinationType == null) throw new ArgumentNullException("destinationType");
            if (dataSource == null) throw new ArgumentNullException("dataSource");

            int ord = 0;

            Stack<MaterializationState> stk = new Stack<MaterializationState>();
            MaterializationState root = new MaterializationState(destinationType, null);
            stk.Push(root);
            
            // Build up the column mappings according to the type:
            while (stk.Count > 0)
            {
                var state = stk.Pop();

                if (state.Type.Name.StartsWith("<>f__AnonymousType"))
                {
                    // Consume a 'test' bit column
                    if (dataSource.GetName(ord).StartsWith("test") && (dataSource.GetFieldType(ord) == typeof(int)))
                    {
                        state.NullTestOrdinal = ord++;
                    }

                    // Use ctor parameters for materializing anonymous types:
                    var ctors = state.Type.GetConstructors();
                    Debug.Assert(ctors.Length == 1);
                    
                    // Enumerate ctor parameters in reverse declaration order (for stack):
                    var prms = ctors[0].GetParameters();
                    for (int i = prms.Length - 1; i >= 0; --i)
                    {
                        stk.Push(new CtorParameterMaterializationState(prms[i].ParameterType, state, prms[i]));
                    }
                }
                else if (!state.Type.IsPrimitive && state.Type.IsClass && !state.Type.IsAbstract && (state.Type != typeof(string)))
                {
                    // Consume a 'test' bit column
                    if (dataSource.GetName(ord).StartsWith("test") && (dataSource.GetFieldType(ord) == typeof(int)))
                    {
                        state.NullTestOrdinal = ord++;
                    }

                    // Enumerate public writable properties in reverse declaration order (for stack):
                    var props = state.Type.GetProperties();
                    for (int i = props.Length - 1; i >= 0; --i)
                    {
                        if (props[i].GetSetMethod() == null) continue;
                        stk.Push(new PropertyMaterializationState(props[i].PropertyType, state, props[i]));
                    }
                }
                else if (!state.Type.IsPrimitive && state.Type.IsValueType && !state.Type.IsAbstract)
                {
                    // Figure something out here; probably assigning writable properties again.
                    throw new NotImplementedException();
                }
                else
                {
                    // Assume a primitive type that can be read directly off the DbDataReader with GetValue(int).

                    // Assume the columns are ordered by property/parameter declaration order.
                    // Assume that all properties/parameters declared have a column to map to.
                    // TODO: Pick up the TEST bit columns used to generate null references with.

                    Debug.Assert(ord < dataSource.FieldCount);
                    Debug.Assert(dataSource.GetName(ord).StartsWith(state.Name, StringComparison.OrdinalIgnoreCase));

                    state.SourceOrdinal = ord++;

                    // NOTE: We can do better via the naming convention of `ColumnName###` where `###` is an integer value
                    // used to distinguish between repeated usages of the same column name. Simply tracking the incrementing
                    // integer values per column name by moving forward through the ordinals should work.
                }
            }

            Debug.Assert(ord == dataSource.FieldCount);

            return new DbDataReaderObjectMaterializationMapping(dataSource, root);
        }

        public object Materialize(IObjectMaterializationMapping<DbDataReader> source)
        {
            DbDataReaderObjectMaterializationMapping mapping = (DbDataReaderObjectMaterializationMapping)source;

            return mapping.Root.Materialize(source.DataSource);
        }
    }
}
