using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace KernowCode.KTest.Ubaddas
{
    public class Behaviour : IBase, IGiven, IWhen, IThen, IState, ISet
    {
        internal const int LeftSectionPadding = 9;

        private static string _targetApplicationLayer;

        public Behaviour()
        {
            Narrate = true;
        }

        private void As(IPersona persona) //this method invoked via reflection from BehaviourExtensions method
        {
            CurrentPersonaType = GetPersonaLayerType(persona);
            if (Narrate)
                Console.WriteLine(string.Format("{0}{1}", "As".PadLeft(LeftSectionPadding), persona.Name()).ExpandToReadable());
        }

        private Type GetPersonaLayerType(IPersona persona)
        {
            if (_targetApplicationLayer == null)
                return persona.GetType();
            string typeNamespace = null;
            var personaType = persona.GetType();
            typeNamespace = personaType.FullName.TrimEnd(personaType.Name.ToCharArray()) +
                            _targetApplicationLayer;
            typeNamespace = typeNamespace + "." + personaType.Name + ", " + personaType.Assembly;
            var type = Type.GetType(typeNamespace);
            if (type == null)
                throw new NotImplementedException(
                    Environment.NewLine
                    + "Could not create instance of Persona with namespace " + typeNamespace
                    + Environment.NewLine);
            return type;
        }

        private void DoBehaviourSet(string behaviour, Action<ISet> actionDelegate)
        {
            Narrate = false;
            var rememberedPersona = CurrentPersonaType;            
            Console.Write(behaviour.PadLeft(LeftSectionPadding).ExpandToReadable() + " ");
            actionDelegate(this);
            CurrentPersonaType = rememberedPersona;
            Narrate = true;
        }    

        private void DoBehaviour(string behaviour, Action domainEntityCommand)
        {
            if (Narrate)
            {
                var line = "";
                if (domainEntityCommand.Method.Name.Contains("_"))
                    line = domainEntityCommand.Method.Name.Replace("_", " " + domainEntityCommand.Target.Name()).ExpandToReadable();
                else
                    line = string.Format("{0} {1}", domainEntityCommand.Method.Name, domainEntityCommand.Target.Name()).ExpandToReadable();
                Console.WriteLine(string.Format("{0}{1}", behaviour.PadLeft(LeftSectionPadding), line).ExpandToReadable());
            }
            var implementedDomain = CreatePersonaImplementation();
            SetDomainOnPersonaImplementation(domainEntityCommand, implementedDomain);
            var method = GetPersonaImplementedMethod(domainEntityCommand);
            try
            {
                method.Invoke(implementedDomain, null);
            }
            catch (Exception exception)
            {
                if (exception.InnerException is NotImplementedException)
                {
                    throw new NotImplementedException(
                        string.Format(
                            Environment.NewLine
                            + "Pending implementation I{0}.{1} in the {2} class.",
                            domainEntityCommand.Target.GetType().Name,
                            domainEntityCommand.Method.Name, implementedDomain.GetType().FullName) +
                        Environment.NewLine, exception);
                }
                throw;
            }
        }        

        private object CreatePersonaImplementation()
        {
            object asPersona;
            try
            {
                asPersona = Activator.CreateInstance(CurrentPersonaType);
            }
            catch (Exception exception)
            {
                throw new Exception(
                    Environment.NewLine +
                    string.Format("Make sure the '{0}' class has a parameterless constructor.", CurrentPersonaType.Name)
                    + Environment.NewLine, exception);
            }            
            return asPersona;
        }

        private void SetDomainOnPersonaImplementation(Action domainEntityCommand, object persona)
        {
            try
            {
                var entityProperty = persona.GetType().GetProperty(domainEntityCommand.Target.GetType().Name);
                entityProperty.SetValue(persona, domainEntityCommand.Target);
            }
            catch (Exception exception)
            {
                throw new Exception(
                    Environment.NewLine
                    + string.Format("Could not set the '{0}' entity for the '{1}' persona implementation.",
                                    domainEntityCommand.Target.GetType().Name, persona.GetType().Name)
                    + Environment.NewLine
                    + string.Format(
                        "Check you have a public property 'public {0} {0} {{ get; set; }}' in the '{1}' class.",
                        domainEntityCommand.Target.GetType().Name, persona.GetType().Name)
                    + Environment.NewLine, exception);
            }
        }

        private MethodInfo GetPersonaImplementedMethod(Action domainEntityCommand)
        {
            try
            {
                var method = CurrentPersonaType.GetMethod(domainEntityCommand.Method.Name);
                if (method == null)
                    method =
                        CurrentPersonaType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                      BindingFlags.NonPublic)
                            .FirstOrDefault(
                                x =>
                                x.Name.EndsWith(domainEntityCommand.Target.GetType().Name + "." + domainEntityCommand.Method.Name));
                if (method == null)
                    throw new NotImplementedException();
                return method;
            }
            catch(Exception exception)
            {
                throw new Exception(
                    Environment.NewLine
                    + string.Format(
                        "There was a problem calling '{0}' in the '{1}' persona implementation of the '{2}' entity.",
                        domainEntityCommand.Method.Name, CurrentPersonaType.Name, domainEntityCommand.Target.GetType().Name)
                    + Environment.NewLine
                    + string.Format(
                        "Make sure the '{0}' persona implementation implements the '{1}' entity interface including the '{2}' method.",
                        CurrentPersonaType.Name, domainEntityCommand.Target.GetType().Name, domainEntityCommand.Method.Name)
                    + Environment.NewLine, exception);
            }
        }
        
        public IGiven Given(Action domainEntityCommand)
        {
            DoBehaviour("Given", domainEntityCommand);
            return this;
        }
        
        public IWhen When(Action domainEntityCommand)
        {
            DoBehaviour("When", domainEntityCommand);
            return this;
        }
        
        public IThen Then(Action domainEntityCommand)
        {
            DoBehaviour("Then", domainEntityCommand);
            return this;
        }

        public IGiven Given(Action<ISet> actionDelegate)
        {
            DoBehaviourSet("Given", actionDelegate);
            return this;
        }

        public IWhen When(Action<ISet> actionDelegate)
        {
            DoBehaviourSet("When", actionDelegate);
            return this;
        }
        
        public IThen Then(Action<ISet> actionDelegate)
        {
            DoBehaviourSet("Then", actionDelegate);
            return this;
        }

        public static Behaviour SoThat(string businessValue, string targetApplicationLayer)
        {
            _targetApplicationLayer = targetApplicationLayer;
            var testName = GetTestMethodName();
            testName = AddPrefix(testName);            
            Console.WriteLine("{0}", testName.ExpandToReadable());
            Console.WriteLine(string.Format("{0}{1}", "So that".PadLeft(LeftSectionPadding), businessValue).ExpandToReadable());
            return new Behaviour();
        }

        private static string GetTestMethodName()
        {
            var frames = new StackTrace().GetFrames();
            foreach (var stackFrame in frames)
            {
                foreach (var attribute in stackFrame.GetMethod().CustomAttributes)
                {
                    if (new[] { "TestAttribute", "TestMethodAttribute" }.Any(x => x == attribute.AttributeType.Name))
                        return stackFrame.GetMethod().Name;
                }
            }
            throw new Exception("", new Exception("When trying to render test behaviour to console, could not find test method (by custom attribute named 'Test' or 'TestMethod')"));
        }

        private static string AddPrefix(string reason)
        {
            var disallowedPrefixes = new [] {"Should"};
            foreach (var disallowedPrefix in disallowedPrefixes)
                reason = reason.TrimStart(disallowedPrefix.ToCharArray());
            var reasonPrefixes = new[] { "IWantTo", "IWant", "InOrderTo", "InOrder" };
            if (reasonPrefixes.Any(x => reason.ToLower().StartsWith(x.ToLower())))
                return reason;
            return ("IWantTo".PadLeft(LeftSectionPadding) + reason).ExpandToReadable();
        }

        public bool Narrate { get; set; }

        public Type CurrentPersonaType { get; set; }

        public IBase Perform()
        {
            var methodName = new StackTrace().GetFrame(1).GetMethod().Name.ExpandToReadable();
            methodName = Char.ToLowerInvariant(methodName[0]) + methodName.Substring(1);
            Console.WriteLine(methodName);
            return this;
        }
    }
}