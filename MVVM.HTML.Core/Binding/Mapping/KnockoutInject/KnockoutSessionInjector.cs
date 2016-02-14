﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MVVM.HTML.Core.Binding.Mapping;
using MVVM.HTML.Core.Exceptions;
using MVVM.HTML.Core.Infra;
using MVVM.HTML.Core.Binding.Extension;
using MVVM.HTML.Core.JavascriptEngine.JavascriptObject;

namespace MVVM.HTML.Core.HTMLBinding
{
    internal class KnockoutSessionInjector : IJavascriptSessionInjector
    {
        private readonly IWebView _WebView;
        private readonly IJavascriptChangesObserver _IJavascriptListener;
        private readonly Queue<IJavascriptObjectMapper> _IJavascriptMapper = new Queue<IJavascriptObjectMapper>();
        private readonly IDictionary<IJavascriptObject, IDictionary<string, IJavascriptObject>>
                            _Silenters = new Dictionary<IJavascriptObject, IDictionary<string, IJavascriptObject>>();
        private IJavascriptObject _Listener;
        private IJavascriptObjectMapper _Current;
        private IJavascriptObject _Mapper;
        private bool _PullNextMapper = true;

        internal KnockoutSessionInjector(IWebView iWebView, IJavascriptChangesObserver iJavascriptListener)
        {
            _WebView = iWebView;
            _IJavascriptListener = iJavascriptListener;

            _WebView.Run(() =>
                {
                    _Listener = _WebView.Factory.CreateObject(false);

                    if (_IJavascriptListener == null)
                        return;

                    _Listener.Bind("TrackChanges", _WebView, (e) => _IJavascriptListener.OnJavaScriptObjectChanges(e[0], e[1].GetStringValue(), e[2]));
                    _Listener.Bind("TrackCollectionChanges", _WebView, JavascriptColectionChanged);
                });
        }

        public void Dispose()
        {
            _Silenters.Clear();
            _WebView.Run(() =>
            {
                if (_Listener == null)
                    return;

                _Listener.Dispose();
                _Listener = null;
            });
        }

        private void JavascriptColectionChanged(IJavascriptObject[] arguments)
        {
            var values = arguments[1].GetArrayElements();
            var types = arguments[2].GetArrayElements();
            var indexes = arguments[3].GetArrayElements();
            var collectionChange = new JavascriptCollectionChanges(arguments[0], values.Zip(types, indexes,
                                            (v, t, i) => new IndividualJavascriptCollectionChange(
                                                t.GetStringValue() == "added" ? CollectionChangeType.Add : CollectionChangeType.Remove,
                                                i.GetIntValue(), v)));

            _IJavascriptListener.OnJavaScriptCollectionChanges(collectionChange);
        }

        private IJavascriptObject GetMapper(IJavascriptObjectMapper iMapperListener)
        {
            _IJavascriptMapper.Enqueue(iMapperListener);

            if (_Mapper != null)
                return _Mapper;

            _Mapper = _WebView.Factory.CreateObject(false);

            _Mapper.Bind("Register", _WebView, (e) =>
            {
                if (_PullNextMapper)
                {
                    _Current = _IJavascriptMapper.Dequeue();
                    _PullNextMapper = false;
                }

                if (_Current == null)
                    return;

                int count = e.Length;
                var registered = e[0];

                switch (count)
                {
                    case 1:
                        _Current.MapFirst(registered);
                        break;

                    case 3:
                        _Current.Map(e[1], e[2].GetStringValue(), registered);
                        break;

                    case 4:
                        _Current.MapCollection(e[1], e[2].GetStringValue(), e[3].GetIntValue(), registered);
                        break;
                }
            });

            _Mapper.Bind("End", _WebView, (e) =>
                {
                    if (_PullNextMapper)
                        _Current = _IJavascriptMapper.Dequeue();

                    if (_Current != null)
                        _Current.EndMapping(e[0]);
                    _Current = null;
                    _PullNextMapper = true;
                });

            return _Mapper;
        }

        private IJavascriptObject _Ko;
        private IJavascriptObject GetKo()
        {
            if (_Ko == null)
            {
                _Ko = _WebView.GetGlobal().GetValue("ko");
                if ((_Ko == null) || (!_Ko.IsObject))
                    throw ExceptionHelper.NoKo();
            }

            return _Ko;
        }

        public IJavascriptObject Inject(IJavascriptObject ihybridobject, IJavascriptObjectMapper ijvm)
        {
            return _WebView.Evaluate(() =>
                {
                    return GetKo().Invoke("MapToObservable", _WebView, ihybridobject, GetMapper(ijvm), _Listener);
                });
        }

        public Task RegisterMainViewModel(IJavascriptObject iJSObject)
        {
            var ko = GetKo();

            return _WebView.RunAsync(() =>
                {
                    ko.Bind("log", _WebView, (e) => ExceptionHelper.Log(string.Join(" - ", e.Select(s => (s.GetStringValue().Replace("\n", " "))))));
                    ko.Invoke("register", _WebView, iJSObject);
                    ko.Invoke("applyBindings", _WebView, iJSObject);
                });
        }

        public void UpdateProperty(IJavascriptObject father, string propertyName, IJavascriptObject value)
        {
            var silenter = GetSilenter(father, propertyName);
            if (silenter != null)
            {
                Silent(silenter, value);
                return;
            }

            _WebView.RunAsync(() =>
            {
                silenter = GetOrCreateSilenter(father, propertyName);
                Silent(silenter, value);
            });
        }

        private IJavascriptObject GetSilenter(IJavascriptObject father, string propertyName)
        {
            var dic = _Silenters.GetOrDefault(father);
            return (dic == null) ? null : dic.GetOrDefault(propertyName);
        }

        private IJavascriptObject GetOrCreateSilenter(IJavascriptObject father, string propertyName)
        {
            var dic = _Silenters.FindOrCreateEntity(father, _ => new Dictionary<string, IJavascriptObject>());
            return dic.FindOrCreateEntity(propertyName, name => father.GetValue(name));
        }

        private void Silent(IJavascriptObject silenter, IJavascriptObject value)
        {
            silenter.Invoke("silent", _WebView, value);
        }
    }
}
