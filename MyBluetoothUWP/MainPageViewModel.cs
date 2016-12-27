using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.Prism.Mvvm;
using Microsoft.Practices.Prism.Commands;
using System.Collections.Concurrent;
using System.IO;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;

namespace MyBluetoothUWP
{
    public abstract class BluetoothListItemBase : BindableBase
    {
        public string FriendlyName { get; set; } = "";
        public virtual string Description { get { return FriendlyName; } }

        public DelegateCommandBase TakeActionCommand { get { return DelegateCommand.FromAsyncHandler(TakeActionAsync); } }
        public DelegateCommandBase ExportCommand { get { return DelegateCommand.FromAsyncHandler(ExportAsync); } }
        public DelegateCommandBase SendMessageCommand { get { return DelegateCommand<string>.FromAsyncHandler(SendMessageClickAsync); } }

        private ConcurrentQueue<string> loggedQueue = new ConcurrentQueue<string>();
        public abstract Task TakeActionAsync();
        public abstract Task SendMessageClickAsync(string arg);
        public async Task ExportAsync()
        {
            try
            {
                FileSavePicker filePicker = new Windows.Storage.Pickers.FileSavePicker();
                filePicker.DefaultFileExtension = ".csv";
                filePicker.FileTypeChoices.Add("数据文件", new[] { ".csv" });
                filePicker.SuggestedStartLocation = PickerLocationId.Desktop;
                filePicker.SuggestedFileName = DateTime.Now.ToString("yyyyMMddhhmmss");

                StorageFile file = await filePicker.PickSaveFileAsync();
                
                if (file != null)
                {
                    // 在用户完成更改并调用CompleteUpdatesAsync之前，阻止对文件的更新
                    CachedFileManager.DeferUpdates(file);
                    IEnumerable<string> results = DecodeQueue();

                    await FileIO.WriteBytesAsync(file, new byte[] { 0xFF, 0xFE });
                    await FileIO.WriteLinesAsync(file, results, Windows.Storage.Streams.UnicodeEncoding.Utf16LE);
                    // 当完成更改时，其他应用程序才可以对该文件进行更改。
                    FileUpdateStatus updateStatus = await CachedFileManager.CompleteUpdatesAsync(file);
                    Debug.WriteLine("save completed");
                    if (updateStatus == FileUpdateStatus.Complete)
                    {
                        //tBlockSaveInfo.Text = file.Name + " 已经保存好了。";
                    }
                    else
                    {
                        //tBlockSaveInfo.Text = file.Name + " 保存失败了。";
                    }
                }
                else
                {
                    //tBlockSaveInfo.Text = "保存操作被取消。";
                }



                //using (StreamWriter sw = File.CreateText(path))
                //{
                //    while (!loggedQueue.IsEmpty)
                //    {
                //        if (!loggedQueue.TryDequeue(out string result)) break;
                //        await sw.WriteLineAsync(result);
                //    }
                //}
            }
            catch (Exception e)
            {
                AppendLog("系统消息", e.Message);
            }
        }

        IEnumerable<string> DecodeQueue()
        {
            string breaker = "BREAKER" + DateTime.Now.Ticks;
            loggedQueue.Enqueue(breaker);
            while (!loggedQueue.IsEmpty)
            {
                if (!loggedQueue.TryDequeue(out string result) || result == breaker) yield break;
                if (result.StartsWith("BERAKER")) continue;
                yield return result;
            }
        }

        public virtual string Log { get; set; }

        public void AppendLog(string log, string sender = "", bool show = false)
        {
            loggedQueue.Enqueue($"{sender},{DateTime.Now.TimeOfDay.TotalMilliseconds},{log.Replace(',', '，')}");
            if (show)
            {
                Log += $"\n{sender}({DateTime.Now})：\n\t{log}";
                OnPropertyChanged(nameof(Log));
            }
        }
    }

    public class BluetoothServiceItem : BluetoothListItemBase
    {
        public event EventHandler<string> SendingMessage = (o, e) => { };
        public event EventHandler<string> ReceivedMessage = (o, e) => { };

        //private Stream peerStream;
        //private StreamReader reader;
        //private StreamWriter writer;
        private GattCharacteristic _characteristic;
        private BluetoothLEDevice _device;
        private GattDeviceService _service;
        private IReadOnlyList<GattDeviceService> _services;
        private string stringtemp="";

        public BluetoothServiceItem() { }

        public static async Task<BluetoothServiceItem> GetBluetoothServiceItemAsync(DeviceInformation info)
        {

            BluetoothServiceItem item = new BluetoothServiceItem();

            try
            {
                Debug.WriteLine(info.Name);
                Debug.WriteLine(info.Kind);
                Debug.WriteLine(info.Id);
                if (await item.ConnectToDeviceAsync(info) == false)
                {
                    Debug.WriteLine("failinstage1");
                    return null;
                }
                if (await item.FindServiceAsync(info) == false)
                {
                    Debug.WriteLine("failinstage2");
                    return null;
                }
                if (await item.FindCharacteristicAsync(info) == false)
                {
                    Debug.WriteLine("failinstage3");
                    return null;
                }
                Debug.WriteLine("succeeded");

                item.FriendlyName = info.Name;
                Debug.WriteLine(info.Name);
                await item._characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                item._characteristic.ValueChanged += new TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>(item.CharacteristicValueChanged_HandlerAsync);
                Debug.WriteLine(info.Name + " Added");
                return item;
            }
            catch (Exception)
            {
                return null;
                throw;
            }

        }

        private async Task<bool> ConnectToDeviceAsync(DeviceInformation info)
        {
            try
            {
                _device = await BluetoothLEDevice.FromIdAsync(info.Id);
                Debug.WriteLine(_device.ToString());
                return true;
            }
            catch
            {
                Debug.WriteLine("failtoconnect");
                return false;
            }
        }

        private async Task<bool> FindServiceAsync(DeviceInformation info)
        {
            try
            {
                foreach (var se in _device.GattServices)
                {
                    Debug.WriteLine(se.ToString());
                }
                _services = _device.GattServices;
                Debug.WriteLine(GattDeviceService.ConvertShortIdToUuid(0xFFE0).ToString());
                foreach (GattDeviceService s in _services)
                {
                    Debug.WriteLine(s.Uuid.ToString());
                    if (s.Uuid == GattDeviceService.ConvertShortIdToUuid(0xFFE0))
                    {
                        _service = s;
                        Debug.WriteLine("Found service");
                    }
                }
                return true;
            }
            catch
            {
                Debug.WriteLine("serviceerror");
                return false;
            }
        }

        private async Task<bool> FindCharacteristicAsync(DeviceInformation info)
        {
            try
            {
                Debug.WriteLine(GattCharacteristic.ConvertShortIdToUuid(0xFFE1).ToString());
                foreach (GattCharacteristic c in _service.GetAllCharacteristics())
                {
                    Debug.WriteLine(c.Uuid.ToString());
                    if (c.Uuid == GattCharacteristic.ConvertShortIdToUuid(0xFFE1))
                    {
                        _characteristic = c;
                        Debug.WriteLine("Found characteristic");
                    }
                }
                return true;
            }
            catch
            {
                Debug.WriteLine("characteristicerror");
                return false;
            }
        }

        public async void CharacteristicValueChanged_HandlerAsync(GattCharacteristic sender, GattValueChangedEventArgs obj)
        {
            string str;
            str = GetStringFromBuffer(obj.CharacteristicValue);
            stringtemp += str;
            string[] strsp = stringtemp.Split('\n');
            for(var i=0;i<strsp.Count()-1; i++)
            {
                AppendLog(strsp[i], "设备");
                ReceivedMessage(this, strsp[i]);
            }
            stringtemp = strsp.Last();
        }

        public override async Task TakeActionAsync()
        {
#warning 待完成，补全向设备发送命令的方法
            await SendMessageAsync("AAA");
        }

        public async Task SendMessageAsync(string message)
        {
            try
            {
                Debug.WriteLine("startofsendtask");
                Debug.WriteLine(FriendlyName);
                GattCommunicationStatus GCS = await _characteristic.WriteValueAsync(GetBufferFromString(message),GattWriteOption.WriteWithoutResponse);
                Debug.WriteLine(GCS.ToString());
                AppendLog(message, "本机");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                throw;
            }

            //SendingMessage(this, message);
        }

        public override async Task SendMessageClickAsync(string arg)
        {
            await SendMessageAsync(arg);
        }

        private IBuffer GetBufferFromString(String str)
        {
            using (InMemoryRandomAccessStream memoryStream = new InMemoryRandomAccessStream())
            {
                using (DataWriter dataWriter = new DataWriter(memoryStream))
                {
                    dataWriter.WriteString(str);
                    return dataWriter.DetachBuffer();
                }
            }
        }
        //Buffer 转 String
        private String GetStringFromBuffer(IBuffer buffer)
        {
            using (DataReader dataReader = DataReader.FromBuffer(buffer))
            {
                return dataReader.ReadString(buffer.Length);
            }
        }

//        public async Task OnReceiveMessageAsync(string message)
//        {
//#warning 待完成，补全接收到数据时的响应
//            AppendLog(message, "设备");
//            ReceivedMessage(this, message);
//        }
    }

    public class TestBluetoothServiceItem : BluetoothServiceItem
    {
        public TestBluetoothServiceItem(int i)
        {
            FriendlyName = $"测试设备{i}";
        }

        //public override async Task TakeActionAsync()
        //{
        //    await SendMessageAsync("Test");
        //    await OnReceiveMessageAsync("Reply");
        //}
    }

    public class AllBluetoothItem : BluetoothListItemBase
    {
        private List<BluetoothServiceItem> allServices = new List<BluetoothServiceItem>();

        public void Insert(BluetoothServiceItem item)
        {
            if (item != null)
            {
                allServices.Add(item);
                item.SendingMessage += (o, e) =>
                {
                    AppendLog(e, $"本机=>{((BluetoothServiceItem)o).FriendlyName}");
                };
                item.ReceivedMessage += (o, e) =>
                {
                    AppendLog(e, $"{((BluetoothServiceItem)o).FriendlyName}=>本机");
                };
            }
        }

        public override async Task TakeActionAsync()
        {
            foreach (var item in allServices)
                await item.TakeActionAsync();
        }

        public override async Task SendMessageClickAsync(string arg)
        {
            foreach (var item in allServices)
                await item.SendMessageClickAsync(arg);
        }

        public AllBluetoothItem()
        {
            FriendlyName = "全体设备";
        }
    }

    class MainPageViewModel : BindableBase
    {
        public DelegateCommandBase RefreshDeviceCommand { get { return DelegateCommand.FromAsyncHandler(RefreshDeviceAsync); } }
        public DelegateCommandBase SelectionChangedCommand { get { return DelegateCommand<ItemClickEventArgs>.FromAsyncHandler(async args => await AddDeviceAsync((DeviceInformation)args.ClickedItem)); } }

        public ObservableCollection<BluetoothListItemBase> BluetoothDevices { get; private set; }
        public DeviceInformationCollection Devices { get; private set; }
        public DeviceInformationCollection UnpairDevices { get; private set; }
        private AllBluetoothItem allBluetoothItem = new AllBluetoothItem();
        public bool BusyIndicator0 { get; private set; } = false;
        public bool BusyIndicator1 { get; private set; } = false;
        public MainPageViewModel()
        {
            BluetoothDevices = new ObservableCollection<BluetoothListItemBase> { allBluetoothItem };
        }

        private async Task AddDeviceAsync(DeviceInformation info)
        {
            if (info == null)
            {
                Debug.WriteLine("noinfo");
            }
            if (info.Pairing.IsPaired)
            {
                BluetoothServiceItem nitem = await BluetoothServiceItem.GetBluetoothServiceItemAsync(info);
                if (nitem != null)
                {
                    BluetoothDevices.Add(nitem);
                    allBluetoothItem.Insert(nitem);
                }
                else
                {
                    Debug.WriteLine("noitem");
                }
            }
            else
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-bluetooth:", UriKind.RelativeOrAbsolute));
            }


        }

        private async Task RefreshDeviceAsync()
        {
            if (BusyIndicator0|| BusyIndicator1) return;
            
            RefreshDeviceCommand.RaiseCanExecuteChanged();
            Debug.WriteLine("AddDeviceBegin!");
            BusyIndicator0 = true;
            OnPropertyChanged(nameof(BusyIndicator0));
            try
            {
                Devices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
                BusyIndicator0 = false;
                OnPropertyChanged(nameof(BusyIndicator0));
            }
            catch (Exception) { }
            BusyIndicator1 = true;
            OnPropertyChanged(nameof(BusyIndicator1));
            try
            {
                UnpairDevices = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(false));
                BusyIndicator1 = false;
                OnPropertyChanged(nameof(BusyIndicator1));
            }
            catch (Exception) { }
            
            Debug.WriteLine("AddDeviceFinished!");
            OnPropertyChanged(nameof(Devices));

            //BluetoothServiceItem item = new TestBluetoothServiceItem(0);
            //BluetoothDevices.Add(item);
            //allBluetoothItem.Insert(item);
            
        }
    }
}