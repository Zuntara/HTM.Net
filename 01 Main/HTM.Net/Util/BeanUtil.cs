using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HTM.Net.Util
{
    /**
 *Singleton helper for reading/writing beans properties
 *@author Kirill Solovyev
 */
    public class BeanUtil
    {
        //TODO introduce proper log in future
        //private static final Log LOG = LogFactory.getLog(BeanUtil.class);
        private Dictionary<Type, InternalPropertyInfo[]> properties = new Dictionary<Type, InternalPropertyInfo[]>();
        private static readonly MemberInfo[] EMPTY_PROPERTY_DESCRIPTOR = new MemberInfo[0];
        private static BeanUtil INSTANCE = new BeanUtil();

        public static BeanUtil GetInstance()
        {
            return INSTANCE;
        }

        private BeanUtil()
        {
        }

        /**
        * Write value to bean's property
        * @param bean
        * @param name
        * @param value
        * @return
        */
        public bool SetSimpleProperty(object bean, string name, object value)
        {
            InternalPropertyInfo pi = GetPropertyInfo(bean, name);
            if (pi != null)
            {
                SetSimpleProperty(bean, pi, value);
                return true;
            }
            else {
                return false;
            }
        }

        public void SetSimplePropertyRequired(object bean, string name, object value)
        {
            SetSimpleProperty(bean, GetPropertyInfoRequired(bean, name), value);
        }

        /**
         * Return bean's property value
         * @param bean
         * @param name
         * @return
         */
        public object GetSimpleProperty(object bean, string name)
        {
            return GetSimpleProperty(bean, GetPropertyInfo(bean, name));
        }


        private object GetSimpleProperty(object bean, InternalPropertyInfo info)
        {
            if (info.GetReadMethod() == null)
            {
                throw new ArgumentException("Property '" + info.name + "' of bean " + bean.GetType().Name +
                                                   " does not have getter method");
            }
            return InvokeReadMethod(info.GetReadMethod(), bean);
        }

        private void SetSimpleProperty(object bean, InternalPropertyInfo info, object value)
        {
            if (info.GetWriteMethod() == null)
            {
                throw new ArgumentException("Property '" + info.name + "' of bean " + bean.GetType().Name +
                                                   " does not have setter method");
            }
            InvokeWriteMethod(info.GetWriteMethod(), bean, info.GetName(), value);
        }

        private void InvokeWriteMethod(Action<object, object> m, object instance, string name, params object[] args)
        {
            if (instance == null)
            {
                throw new ArgumentException("Can not invole Method/field '" + name + "' on null instance");
            }
            try
            {
                m(instance, args);
            }
            catch (ArgumentException e)
            {
                string msg = "Cannot invoke " + instance.GetType().Name + "." + name + " - " + e.Message;
                //LOG.error(msg, e);
                throw new ArgumentException(msg, e);
            }
            catch (AccessViolationException e)
            {
                string msg = "Cannot invoke " + instance.GetType().Name + "." + name + " - " + e.Message;
                //LOG.error(msg, e);
                throw new InvalidOperationException(msg, e);
            }
            catch (TargetInvocationException te)
            {
                string msg = "Error invoking " + instance.GetType().Name + "." + name + " - " + te.Message;
                //LOG.error(msg, e);
                throw new InvalidOperationException(msg, te);
            }
        }

        private object InvokeReadMethod(Func<object, object> m, object instance)
        {
            if (instance == null)
            {
                throw new ArgumentException("Can not invole Method '" + m + "' on null instance");
            }
            try
            {
                return m(instance);
            }
            catch (ArgumentException e)
            {
                string msg = "Cannot invoke " + instance.GetType().Namespace + "." + m + " - " + e.Message;
                //LOG.error(msg, e);
                throw new ArgumentException(msg, e);
            }
            catch (AccessViolationException e)
            {
                string msg = "Cannot invoke " + instance.GetType().Namespace + "." + m + " - " + e.Message;
                //LOG.error(msg, e);
                throw new InvalidOperationException(msg, e);
            }
            catch (TargetInvocationException te)
            {
                string msg = "Error invoking " + instance.GetType().Namespace + "." + m + " - " + te.Message;
                //LOG.error(msg, e);
                throw new InvalidOperationException(msg, te);
            }
        }

        public InternalPropertyInfo GetPropertyInfo(object bean, string name)
        {
            if (bean == null)
            {
                throw new ArgumentException("Bean can not be null");
            }
            return GetPropertyInfo(bean.GetType(), name);
        }

        public InternalPropertyInfo GetPropertyInfoRequired(object bean, string name)
        {
            InternalPropertyInfo result = GetPropertyInfo(bean, name);
            if (result == null)
            {
                throw new ArgumentException(
                        "Bean " + bean.GetType().Name + " does not have property '" + name + "'");
            }
            return result;
        }

        public InternalPropertyInfo GetPropertyInfo(Type beanClass, string name)
        {
            if (name == null)
            {
                throw new ArgumentException("Property name is required and can not be null");
            }
            InternalPropertyInfo[] infos = GetPropertiesInfoForBean(beanClass);
            return infos.FirstOrDefault(info => name.Equals(info.GetName(), StringComparison.CurrentCultureIgnoreCase));
        }


        public InternalPropertyInfo[] GetPropertiesInfoForBean(Type beanClass)
        {
            if (beanClass == null)
            {
                throw new ArgumentException("Bean class is required and can not be null");
            }

            InternalPropertyInfo[] infos = null;
            if (properties.ContainsKey(beanClass))
            {
                infos = properties[beanClass];
            }
            if (infos != null)
            {
                return infos;
            }

            MemberInfo[] descriptors;
            try
            {
                descriptors = GetDescriptors(beanClass);
                //descriptors =  Introspector.getBeanInfo(beanClass).getPropertyDescriptors();
                if (!descriptors.Any())
                {
                    descriptors = EMPTY_PROPERTY_DESCRIPTOR;
                }
            }
            catch (Exception)
            {
                descriptors = EMPTY_PROPERTY_DESCRIPTOR;
            }
            infos = new InternalPropertyInfo[descriptors.Length];
            for (int i = 0; i < descriptors.Length; i++)
            {
                infos[i] = CreatePropertyInfo(beanClass, descriptors[i]);
            }
            properties.Add(beanClass, infos);
            return infos;
        }

        private MemberInfo[] GetDescriptors(Type beanClass, MemberInfo[] union = null)
        {
            bool recurseDown = !(beanClass.BaseType != null && beanClass.BaseType.Namespace != null &&
                !beanClass.BaseType.Namespace.StartsWith("htm", StringComparison.InvariantCultureIgnoreCase));

            MemberInfo[] descriptors = beanClass
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Union(beanClass.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(fi => !fi.Name.Contains("<")).Cast<MemberInfo>())
                    .ToArray();

            if (union != null)
            {
                descriptors = descriptors.Union(union).ToArray();
            }

            // We may go down (or up)
            if (recurseDown)
            {
                // recurse down
                var members = GetDescriptors(beanClass.BaseType, descriptors);
                return members;
            }

            return descriptors;
        }

        private InternalPropertyInfo CreatePropertyInfo(Type beanClass, MemberInfo d)
        {
            if (d.MemberType == MemberTypes.Property)
            {
                PropertyInfo pi = (PropertyInfo)d;
                return new InternalPropertyInfo(beanClass, pi.Name, pi.PropertyType
                    , (o) => pi.GetGetMethod().Invoke(o, null), (obj, val) =>
                    {
                        if (val.GetType().IsArray)
                        {
                            pi.GetSetMethod().Invoke(obj, (object[])val);
                        }
                        else
                        {
                            pi.GetSetMethod().Invoke(obj, new[] { val });
                        }

                    });
            }
            if (d.MemberType == MemberTypes.Field)
            {
                FieldInfo fi = (FieldInfo)d;
                MethodInfo miSet = beanClass.GetMethods().FirstOrDefault(m => m.Name.Equals("Set" + fi.Name.TrimStart('_'), StringComparison.InvariantCultureIgnoreCase));
                MethodInfo miGet = beanClass.GetMethods().FirstOrDefault(m => m.Name.Equals("Get" + fi.Name.TrimStart('_'), StringComparison.InvariantCultureIgnoreCase));
                if (miSet == null || miGet == null)
                {
                    return new InternalPropertyInfo(beanClass, fi.Name, d.ReflectedType
                        , (o) => fi.GetValue(o), (obj, val) =>
                        {
                            if(fi.FieldType.IsArray) fi.SetValue(obj, val);
                            else fi.SetValue(obj, ((object[])val).FirstOrDefault());
                        });
                }
                return new InternalPropertyInfo(beanClass, fi.Name, d.ReflectedType
                    , (o) => miGet.Invoke(o, null), (obj, val) =>
                    {
                        if (val.GetType().IsArray)
                        {
                            miSet.Invoke(obj, (object[])val);
                        }
                        else
                        {
                            miSet.Invoke(obj, new[] { val });
                        }
                    });
            }
            throw new InvalidOperationException("Property info could not be created for " + d.Name);
        }

        //@SuppressWarnings("unused")
        private InternalPropertyInfo CreatePropertyInfo(Type beanClass, string propertyName, Type propertyType)
        {
            return new InternalPropertyInfo(beanClass, propertyName, propertyType, null, null);
        }

        public class InternalPropertyInfo
        {
            internal readonly Type beanClass;
            internal readonly string name;
            internal readonly Type type;
            internal readonly Func<object, object> readMethod;
            internal readonly Action<object, object> writeMethod;

            //internal InternalPropertyInfo(Type beanClass, string name, Type type, MethodInfo readMethod, MethodInfo writeMethod)
            //{
            //    this.beanClass = beanClass;
            //    this.name = name.TrimStart('_');
            //    this.type = type;
            //    this.readMethod = readMethod;
            //    this.writeMethod = writeMethod;
            //}

            public InternalPropertyInfo(Type beanClass, string name, Type type, Func<object, object> get, Action<object, object> set)
            {
                this.beanClass = beanClass;
                this.name = name.TrimStart('_');
                this.type = type;
                this.readMethod = get;
                this.writeMethod = set;
            }

            public Type GetBeanClass()
            {
                return beanClass;
            }

            public string GetName()
            {
                return name;
            }

            public new Type GetType()
            {
                return type;
            }

            public Func<object, object> GetReadMethod()
            {
                return readMethod;
            }

            public Action<object, object> GetWriteMethod()
            {
                return writeMethod;
            }


            public override string ToString()
            {
                return "InternalPropertyInfo{" +
                       "beanClass=" + beanClass +
                       ", name='" + name + "'" +
                       ", type=" + type +
                       ", readMethod=" + readMethod +
                       ", writeMethod=" + writeMethod +
                       "}";
            }
        }
    }
}
