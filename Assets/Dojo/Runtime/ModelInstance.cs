using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dojo.Starknet;
using Dojo.Torii;
using dojo_bindings;
using UnityEngine;
using UnityEngine.Events;
using System.Numerics;
using System.Reflection;

namespace Dojo
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ModelField : Attribute
    {
        public string Name { get; }

        public ModelField(string name)
        {
            Name = name;
        }
    }

    // Base class for the definition of a model
    public abstract class ModelInstance : MonoBehaviour
    {
        public UnityEvent OnUpdated = new UnityEvent();
        public Model Model { get; private set; }

        // Initialize the model instance with the model
        // Uses the ModelField attribute to map the model fields to the class fields
        // One can override this method to do custom initialization
        // If reflection isn't an option.
        // Called upon instantiation and model update
        public virtual void Initialize(Model model)
        {
            Model = model;

            var fields = GetType().GetFields();
            foreach (var field in fields)
            {
                // Check if the field has the ModelField attribute
                var attribute = field.GetCustomAttributes(typeof(ModelField), false);
                if (attribute.Length == 0)
                {
                    continue;
                }

                var modelField = (ModelField)attribute[0];
                var member = model.Members[modelField.Name];

                field.SetValue(this, HandleField(field.FieldType, member));
            }
        }

        // Handles the initialization of a field
        // of a model instance. Uses reflection to set the field
        // to the value of the model member.
        private static object HandleField(Type type, object value)
        {
            // if the field is a primitive, we can just set it
            // fieldelement is included as a primitive because its a class
            // but its already instantiated
            if (type.IsPrimitive || type == typeof(FieldElement) || type == typeof(BigInteger) || type == typeof(string))
            {
                return Convert.ChangeType(value, type);
            }
            // handle array
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var array = (IList)value;
                var instance = Array.CreateInstance(elementType, array.Count);
                for (var i = 0; i < array.Count; i++)
                {
                    instance.SetValue(HandleField(elementType, array[i]), i);
                }
                return instance;
            }
            // handle tuple (ValueTuple - (T1, T2))
            else if (type.FullName.StartsWith(typeof(ValueTuple).FullName))
            {
                var tupleTypes = type.GetGenericArguments();
                var instance = Activator.CreateInstance(type);
                var fields = type.GetFields();
                for (var i = 0; i < fields.Length; i++)
                {
                    fields[i].SetValue(instance, HandleField(tupleTypes[i], ((IList)value)[i]));
                }
                return instance;
            }
            // handle record (rust-like) enums
            else if (value is (string, object)) {
                var (variant, member) = ((string, object))value;
                
                var variantType = type.GetNestedType(variant);
                if (variantType == null)
                {
                    throw new Exception($"Could not find variant {variant} in enum {type}");
                }

                if (type.GenericTypeArguments.Length > 0) {
                    variantType = variantType.MakeGenericType(type.GenericTypeArguments);
                }

                List<object> args = new List<object>();
                if (variantType.GetProperty("value") is PropertyInfo prop) {
                    args.Add(HandleField(prop.PropertyType, member));
                }

                return Activator.CreateInstance(variantType, args.ToArray());

            }
            // if the field is a struct/class. we check if our member is a dictionary
            // and we go through each of its keys and values and set them to the fields
            // of the instantiated struct/class
            else if (value is Dictionary<string, object> dict) {
                var instance = Activator.CreateInstance(type);
                var fields = type.GetFields();

                foreach (var field in fields)
                {
                    field.SetValue(instance, HandleField(field.FieldType, dict[field.Name]));
                }

                return instance;
            } else {
                throw new Exception($"Could not handle field of type {type}");
            }
        }

        public static Model ToModel<T>(T model) where T : ModelInstance
        {
            var members = new Dictionary<string, object>();
            foreach (var field in model.GetType().GetFields())
            {
                var attribute = field.GetCustomAttributes(typeof(ModelField), false);
                if (attribute.Length == 0)
                {
                    continue;
                }

                var modelField = (ModelField)attribute[0];
                members.Add(modelField.Name, field.GetValue(model));
            }

            return new Model(model.GetType().Name, members);
        }

        public Model ToModel()
        {
            return ToModel(this);
        }

        // Called when the model is updated
        public virtual void OnUpdate(Model model)
        {
            Initialize(model);
            OnUpdated.Invoke();
        }
    }
}
