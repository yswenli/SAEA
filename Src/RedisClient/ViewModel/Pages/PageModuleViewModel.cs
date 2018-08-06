using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace RedisClient
{
    [Export(typeof(PageModuleViewModel))]
    public class PageModuleViewModel:PropertyChangedBase
    {
        [ImportingConstructor]
        public PageModuleViewModel(RedisClientDetailViewModel clientPage,KeyContainerViewModel keyValueContainerPage)
        {
            this.Items = new ObservableCollection<PageViewModel>();
            this.Items.Add(clientPage);
            this.Items.Add(keyValueContainerPage);
            this.Items.ToList().ForEach(page =>
            {
                page.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "IsVisible")
                    {
                        var vm = s as PageViewModel;
                        if (vm.IsVisible && this.Items.Where(x => x.IsVisible).Count() == 1)
                            this.SelectedItem = vm;
                    }
                };
            });        
        }


        public ObservableCollection<PageViewModel> Items { get; private set; }

        private PageViewModel _selectedItem;

        public PageViewModel SelectedItem
        {
            get { return this._selectedItem; }
            set
            {
                this._selectedItem = value;
                this.NotifyOfPropertyChange(() => this.SelectedItem);
            }
        }

    }
}
