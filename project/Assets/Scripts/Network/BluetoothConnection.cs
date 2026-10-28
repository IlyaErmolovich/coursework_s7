using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
namespace FrostDuel {
    [Serializable] public sealed class BluetoothDevice { public string name,address; }
    [Serializable] public sealed class DeviceList { public BluetoothDevice[] devices; }
    public sealed class BluetoothConnection : MonoBehaviour {
        public event Action Connected,Disconnected;
        public event Action<string> Message,Status;
        public readonly List<BluetoothDevice> Devices=new List<BluetoothDevice>();
        public bool IsConnected {get;private set;}
        public bool Working {get;private set;}
        public bool Attempting => attempting;
        public string Address {get;private set;}
        readonly ConcurrentQueue<Action> events=new ConcurrentQueue<Action>();
        readonly ConcurrentQueue<byte[]> outgoing=new ConcurrentQueue<byte[]>();
        readonly LineFramer framer=new LineFramer();
        float lastReceive,lastPing,attemptStarted;
        bool attempting,initialized,writerRunning;
        int epoch;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject handler;
        Proxy proxy;
#endif
        public void Host() {StartCoroutine(Prepare(true,null));}
        public void Scan() {StartCoroutine(Prepare(false,null));}
        public void Connect(string address) {StartCoroutine(Prepare(false,address));}
        IEnumerator Prepare(bool host,string address) {
            if(Working || attempting)yield break;Working=true;
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var version=new AndroidJavaClass("android.os.Build$VERSION")) {
                string[] permissions=version.GetStatic<int>("SDK_INT")>=31 ? new[]{"android.permission.BLUETOOTH_SCAN","android.permission.BLUETOOTH_CONNECT","android.permission.BLUETOOTH_ADVERTISE"} : new[]{Permission.FineLocation};
                var missing=new List<string>();foreach(var p in permissions)if(!Permission.HasUserAuthorizedPermission(p))missing.Add(p);
                if(missing.Count>0) {
                    Permission.RequestUserPermissions(missing.ToArray());
                    float until=Time.realtimeSinceStartup+45;
                    do {yield return null;missing.RemoveAll(p=>Permission.HasUserAuthorizedPermission(p));}while(missing.Count>0 && Time.realtimeSinceStartup<until);
                    if(missing.Count>0){Status?.Invoke("Разрешите Bluetooth / геолокацию в настройках приложения, затем повторите.");Working=false;yield break;}
                }
            }
            try {
                if(!initialized) {
                    using(var cls=new AndroidJavaClass("com.benstudio.blueunity.BluetoothHandler"))handler=cls.CallStatic<AndroidJavaObject>("getInstance");
                    proxy=new Proxy(this);handler.Call("registerCallbackEvents",proxy);handler.Call("SetPairing",true);initialized=true;
                }
                if(!handler.Call<bool>("isEnabled")) {Status?.Invoke("Включите Bluetooth в настройках телефона и повторите.");Working=false;yield break;}
                framer.Clear();
                if(host) {handler.Call("stopScanForDevices");handler.Call("startServer");handler.Call("startDiscoverable",300);Status?.Invoke("Ожидание второго телефона. Разрешите видимость Bluetooth.");}
                else if(address!=null) {Address=address;attempting=true;attemptStarted=Time.realtimeSinceStartup;handler.Call("stopScanForDevices");handler.Call("connectAsClient",address);Status?.Invoke("Подключение… Подтвердите сопряжение на обоих телефонах.");}
                else {
                    Devices.Clear();var list=JsonUtility.FromJson<DeviceList>(handler.Call<string>("getPairedDevices"));
                    if(list?.devices!=null)foreach(var d in list.devices)AddDevice(d.name,d.address);
                    handler.Call("scanForDevices");Status?.Invoke("Поиск… Выберите телефон ведущего. Android 8–11: включите геолокацию.");
                }
            } catch(Exception e) {Status?.Invoke("Bluetooth: "+e.Message);}
#else
            Status?.Invoke("Bluetooth доступен только в Android-сборке. В редакторе используйте локальную проверку.");
            yield return null;
#endif
            Working=false;
        }
        void AddDevice(string name,string address) {if(!Devices.Exists(d=>d.address==address))Devices.Add(new BluetoothDevice{name=name,address=address});}
        void Update() {
            for(int i=0;i<256 && events.TryDequeue(out var e);i++)e();
            if(attempting && Time.realtimeSinceStartup-attemptStarted>25) {attempting=false;Status?.Invoke("Подключение не завершилось. Проверьте сопряжение и повторите.");}
            if(!IsConnected)return;
            if(Time.realtimeSinceStartup-lastReceive>12) {Drop();Status?.Invoke("Соединение не отвечает. Матч приостановлен.");return;}
            if(Time.realtimeSinceStartup-lastPing>2) {lastPing=Time.realtimeSinceStartup;Send("{\"type\":\"ping\"}");}
        }
        void Up(string address) {epoch++;framer.Clear();Address=address;IsConnected=true;attempting=false;lastReceive=lastPing=Time.realtimeSinceStartup;Connected?.Invoke();}
        void Down() {bool was=IsConnected;IsConnected=false;attempting=false;framer.Clear();if(was)Disconnected?.Invoke();}
        void Receive(byte[] bytes) {
            if(!IsConnected)return;
            lastReceive=Time.realtimeSinceStartup;
            try {framer.Feed(bytes,s=>Message?.Invoke(s));}catch(Exception e){Status?.Invoke(e.Message);Drop();}
        }
        public void Send(string message) {
            if(!IsConnected)return;
            outgoing.Enqueue(Encoding.UTF8.GetBytes(message+"\n"));
#if UNITY_ANDROID && !UNITY_EDITOR
            if(writerRunning)return;writerRunning=true;int generation=epoch;
            System.Threading.Tasks.Task.Run(()=> {
                AndroidJNI.AttachCurrentThread();
                try {while(generation==epoch && outgoing.TryDequeue(out var data))handler.Call("write",Array.ConvertAll(data,b=>unchecked((sbyte)b)));}
                catch(Exception e){events.Enqueue(()=>{Status?.Invoke(e.Message);Drop();});}
                finally {AndroidJNI.DetachCurrentThread();events.Enqueue(()=>{writerRunning=false;if(!outgoing.IsEmpty && IsConnected)Send("{\"type\":\"ping\"}");});}
            });
#endif
        }
        public void Drop() {
            StopAllCoroutines();Working=false;
            epoch++;while(outgoing.TryDequeue(out _)){};
#if UNITY_ANDROID && !UNITY_EDITOR
            try {handler?.Call("stopScanForDevices");handler?.Call("disconnect");}catch(Exception e){Debug.LogWarning(e.Message);}
#endif
            Down();
        }
        void OnDestroy() {
            Drop();
#if UNITY_ANDROID && !UNITY_EDITOR
            try {handler?.Call("cleanup");handler?.Dispose();}catch(Exception e){Debug.LogWarning(e.Message);}
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        [Preserve] sealed class Proxy : AndroidJavaProxy {
            readonly BluetoothConnection c;
            public Proxy(BluetoothConnection c):base("com.benstudio.blueunity.ICallBack"){this.c=c;}
            [Preserve] public void onDiscoveryStarted(){}
            [Preserve] public void onDiscoveryFinished(){}
            [Preserve] public void onDiscoveryDeviceFound(string name,string address)=>c.events.Enqueue(()=>c.AddDevice(name,address));
            [Preserve] public void onConnecting(string address){}
            [Preserve] public void onConnectDeviceNotFound(string address)=>c.events.Enqueue(()=>c.Status?.Invoke("Устройство не найдено: "+address));
            [Preserve] public void onConnected(string address)=>c.events.Enqueue(()=>c.Up(address));
            [Preserve] public void onDisconnected(string address)=>c.events.Enqueue(c.Down);
            [Preserve] public void onDataReceived(sbyte[] data){var bytes=Array.ConvertAll(data,b=>unchecked((byte)b));c.events.Enqueue(()=>c.Receive(bytes));}
            [Preserve] public void onDataNotSent(sbyte[] data)=>c.events.Enqueue(c.Drop);
            [Preserve] public void onError(string error)=>c.events.Enqueue(()=>c.Status?.Invoke("Bluetooth: "+error));
        }
#endif
    }
}
