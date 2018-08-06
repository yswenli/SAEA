using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Threading;
using Caliburn.Micro;



namespace RedisClient
{
 
    public class MefBootstrapper
    {
        readonly bool useApplication;
        bool isInitialized;
        private CompositionContainer _container;
        protected Application Application { get; set; }


        public MefBootstrapper()
        {
       
            this.useApplication = true;
            this.Initialize();
        }


        public void Initialize()
        {
            if (isInitialized)
                return;
            isInitialized = true;
            PlatformProvider.Current = new XamlPlatformProvider();
            if (Execute.InDesignMode)
            {
                try
                {
                    StartDesignTime();
                }
                catch
                {
                    isInitialized = false;
                    throw;
                }
            }
            else
                StartRuntime();
        }

        protected virtual void StartDesignTime()
        {
       
            AssemblySource.Instance.Clear();
            AssemblySource.Instance.AddRange(SelectAssemblies());
            Configure();
            IoC.GetInstance = GetInstance;
            IoC.GetAllInstances = GetAllInstances;
            IoC.BuildUp = BuildUp;
        }


        protected virtual void StartRuntime()
        {
       
            EventAggregator.HandlerResultProcessing = (target, result) => {
                var task = result as System.Threading.Tasks.Task;
                if (task != null)
                {
                    result = new IResult[] { task.AsResult() };
                }
                var coroutine = result as IEnumerable<IResult>;
                if (coroutine != null)
                {
                    var viewAware = target as IViewAware;
                    var view = viewAware != null ? viewAware.GetView() : null;
                    var context = new CoroutineExecutionContext { Target = target, View = view };
                    Coroutine.BeginExecute(coroutine.GetEnumerator(), context);
                }
            };
            AssemblySourceCache.Install();
            foreach (var ambly in SelectAssemblies())
            {
                try
                {
                    //过滤掉win32非托管dll
                    string[] errors = new string[] { "CSkin", "Em.MCIFrameWork", "Itenso" };
                    if (errors.FirstOrDefault(x => ambly.FullName.Contains(x)) == null)
                        AssemblySource.Instance.Add(ambly);
                }
                catch
                {
                }
            };

            if (useApplication)
            {
                Application = Application.Current;
                PrepareApplication();
            }
            Configure();
            IoC.GetInstance = GetInstance;
            IoC.GetAllInstances = GetAllInstances;
            IoC.BuildUp = BuildUp;
        }

        protected virtual void PrepareApplication()
        {
            Application.Startup += OnStartup;
        }

        protected virtual void Configure()
        {
            var catalogs = AssemblySource.Instance.Select(x => new AssemblyCatalog(x)).Cast<ComposablePartCatalog>().ToList();
            var wd = Directory.GetCurrentDirectory();
            var aggCatalog = new AggregateCatalog(catalogs);
            _container = new CompositionContainer(aggCatalog);
            var batch = new CompositionBatch();

            //下面必须EventAggregator实例
            batch.AddExportedValue<IEventAggregator>(new EventAggregator());
            batch.AddExportedValue<IWindowManager>(new WindowManager());
            _container.Compose(batch);
        }


        protected virtual IEnumerable<Assembly> SelectAssemblies()
        {
            List<Assembly> allAssemblies = new List<Assembly>();
            string path = Assembly.GetExecutingAssembly().Location;
            foreach (string dll in Directory.GetFiles(Directory.GetParent(path).FullName, "*.dll"))
                try
                {
                    allAssemblies.Add(Assembly.LoadFile(dll));
                }
                catch { }
            allAssemblies.Add(Assembly.GetExecutingAssembly());
            return allAssemblies;
        }


        protected virtual object GetInstance(Type serviceType, string key)
        {
             
                var contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(serviceType) : key;
                var exports = _container.GetExportedValues<object>(contract);

                if (exports.Count() > 0)
                    return exports.First();

                throw new Exception(string.Format("Could not locate any instances of contract {0}.", contract));
          
        }


        protected virtual IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return _container.GetExportedValues<object>(AttributedModelServices.GetContractName(serviceType));
        }


        protected virtual void BuildUp(object instance)
        {
            _container.SatisfyImportsOnce(instance);
        }

        protected virtual void OnStartup(object sender, StartupEventArgs e)
        {
         
            this.DisplayRootViewFor<ShellViewModel>();
 
 
    
        }



        protected void DisplayRootViewFor(Type viewModelType, IDictionary<string, object> settings = null)
        {
      
            var windowManager = IoC.Get<IWindowManager>();
            windowManager.ShowWindow(IoC.GetInstance(viewModelType, null), null, settings);
        }


        protected void DisplayRootViewFor<TViewModel>(IDictionary<string, object> settings = null)
        {
            DisplayRootViewFor(typeof(TViewModel), settings);
        }

    }

}

