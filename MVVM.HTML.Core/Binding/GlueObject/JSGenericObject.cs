using System.Collections.Generic;
using System.Linq;
using System.Text;
using MVVM.HTML.Core.Infra;
using MVVM.HTML.Core.JavascriptEngine.JavascriptObject;
using MVVM.HTML.Core.Binding.Listeners;
using System.ComponentModel;
using System;

namespace MVVM.HTML.Core.HTMLBinding
{
    public class JSGenericObject : GlueBase, IJSObservableBridge, IListener
    {
        private readonly IWebView _WebView;
        private IJavascriptObject _MappedJSValue;
        private readonly Dictionary<string, IJSCSGlue> _Attributes = new Dictionary<string, IJSCSGlue>();

        public IReadOnlyDictionary<string, IJSCSGlue> Attributes { get { return _Attributes; } }
        public IJavascriptObject JSValue { get; private set; }
        public IJavascriptObject MappedJSValue { get { return _MappedJSValue; } }
        public object CValue { get; private set; }
        public JSCSGlueType Type { get { return JSCSGlueType.Object; } }

        public JSGenericObject(IWebView context, IJavascriptObject value, object icValue)
        {
            JSValue = value;
            CValue = icValue;
            _WebView = context;
        }

        private JSGenericObject(IWebView context, IJavascriptObject value)
        {
            JSValue = value;
            _MappedJSValue = value;
            CValue = null;
            _WebView = context;
        }

        public static JSGenericObject CreateNull(IWebView context)
        {
            return new JSGenericObject(context, context.Factory.CreateNull());
        }

        protected override void ComputeString(StringBuilder sb, HashSet<IJSCSGlue> alreadyComputed)
        {
            sb.Append("{");

            bool f = true;
            foreach (var it in _Attributes.Where(kvp => kvp.Value.Type != JSCSGlueType.Command))
            {
                if (!f)
                    sb.Append(",");

                sb.Append(string.Format(@"""{0}"":", it.Key));

                f = false;
                it.Value.BuilString(sb, alreadyComputed);
            }

            sb.Append("}");
        }

        public void SetMappedJSValue(IJavascriptObject ijsobject)
        {
            _MappedJSValue = ijsobject;
        }

        public IEnumerable<IJSCSGlue> GetChildren()
        {
            return _Attributes.Values; 
        }

        public void UpdateCSharpProperty(string propertyName, IJSCSGlue glue)
        {
            _Attributes[propertyName] = glue;
        }

        public void Listen()
        {
            var notifier = GetObservable();
            if (notifier != null)
                notifier.PropertyChanged += CSharpPropertyChanged;
        }

        public void UnListen()
        {
            var notifier = GetObservable();
            if (notifier != null)
                notifier.PropertyChanged -= CSharpPropertyChanged;
        }

        private INotifyPropertyChanged GetObservable()
        {
            return CValue as INotifyPropertyChanged;  
        }

        private async void CSharpPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var pn = e.PropertyName;
            var propertyAccessor = new PropertyAccessor(sender, pn);
            if (!propertyAccessor.IsGettable)
                return;

            var nv = propertyAccessor.Get();
            var currentChild = Attributes[pn];

            if (Object.Equals(nv, currentChild.CValue))
                return;

            var newbridgedchild = _JSObjectBuilder.Map(nv);
            await RegisterAndDo(newbridgedchild, () =>
            {
                currentfather.UpdateCSharpProperty(pn, newbridgedchild);
                _sessionInjector.UpdateProperty(currentfather.GetJSSessionValue(), pn, newbridgedchild.GetJSSessionValue());
            });
        }
    }
}
