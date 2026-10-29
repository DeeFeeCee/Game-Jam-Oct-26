#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GameJam.Movement;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace GameJam.Presentation
{
    /// <summary>Opt-in, bounded diagnostic capture. Normal play never creates this component.</summary>
    public sealed class FrameTimingProbe : MonoBehaviour
    {
        private struct Sample
        {
            public double time;
            public float delta, playerX, cameraX;
            public long main, render, allocated, physics, animation;
            public int collections, phase;
            public bool focus, looking;
        }
        private readonly Sample[] samples=new Sample[300000];
        private readonly List<ProfilerRecorder> recorders=new List<ProfilerRecorder>();
        private ProfilerRecorder main, render, allocated, physics, animatorMarker;
        private int count, collections;
        private double started;
        private string path;
        private PlayerMovement2D player;
        private PlayerMovementInput2D input;
        private PlayerPresentation presentation;
        private Camera cameraTarget;
        private bool inputWasEnabled, background, capturing;
        private float direction=1;
        public static bool Complete { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Complete=false;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void FromCommandLine()
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++) if(args[i]=="-frame-probe")
                new GameObject("Opt-in frame timing probe").AddComponent<FrameTimingProbe>().Begin(args[i+1]);
        }
        public void Begin(string output)
        {
            Complete=false; path=Path.GetFullPath(output);
            player=FindFirstObjectByType<PlayerMovement2D>();
            input=player.GetComponent<PlayerMovementInput2D>();
            presentation=player.GetComponentInChildren<PlayerPresentation>();
            cameraTarget=Camera.main;
            inputWasEnabled=input.enabled; input.enabled=false;
            background=Application.runInBackground; Application.runInBackground=true;
            var handles=new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var available=new StringBuilder("name,category,unit\n");
            foreach(var handle in handles)
            {
                var description=ProfilerRecorderHandle.GetDescription(handle);
                available.AppendLine(description.Name.Replace(',', ';')+","+description.Category.Name+","+description.UnitType);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path+".counters.csv",available.ToString());
            main=Open("Main Thread"); render=Open("Render Thread"); allocated=Open("GC Allocated In Frame");
            physics=Open("Physics2D.Simulate"); animatorMarker=Open("Animator.Update");
            started=Time.realtimeSinceStartupAsDouble; collections=GC.CollectionCount(0); capturing=true;

            ProfilerRecorder Open(string name)
            {
                foreach(var handle in handles)
                {
                    var description=ProfilerRecorderHandle.GetDescription(handle);
                    if(description.Name!=name) continue;
                    var recorder=ProfilerRecorder.StartNew(description.Category,name,1);
                    recorders.Add(recorder); return recorder;
                }
                return default;
            }
        }
        private void Update()
        {
            if(!capturing) return;
            double elapsed=Time.realtimeSinceStartupAsDouble-started;
            int phase=elapsed<15 ? 0 : elapsed<25 ? 1 : 2;
            if(phase==1)
            {
                if(player.transform.position.x>-2.3f) direction=-1;
                if(player.transform.position.x<-7) direction=1;
                player.SetHorizontalInput(direction);
            }
            else player.SetHorizontalInput(0);
            if(elapsed>=5 && count<samples.Length)
            {
                samples[count++]=new Sample {time=elapsed-5,delta=Time.unscaledDeltaTime,
                    main=Value(main),render=Value(render),allocated=Value(allocated),physics=Value(physics),animation=Value(animatorMarker),
                    collections=GC.CollectionCount(0)-collections,phase=phase,focus=Application.isFocused,
                    looking=presentation.IsLooking,playerX=player.transform.position.x,cameraX=cameraTarget.transform.position.x};
            }
            if(elapsed>=35 || count==samples.Length) Finish();
        }
        private static long Value(ProfilerRecorder recorder) => recorder.Valid&&recorder.Count>0 ? recorder.LastValue : -1;
        private void Finish()
        {
            capturing=false; Release();
            var csv=new StringBuilder("seconds,delta_ms,main_ns,render_ns,gc_bytes,physics_ns,animation_ns,gc0_count,phase,focused,looking,player_x,camera_x\n");
            var culture=CultureInfo.InvariantCulture;
            for(int i=0;i<count;i++)
            {
                Sample s=samples[i];
                csv.Append(s.time.ToString("F6",culture)).Append(',').Append((s.delta*1000).ToString("F4",culture)).Append(',')
                    .Append(s.main).Append(',').Append(s.render).Append(',').Append(s.allocated).Append(',').Append(s.physics).Append(',')
                    .Append(s.animation).Append(',').Append(s.collections).Append(',').Append(s.phase).Append(',')
                    .Append(s.focus?1:0).Append(',').Append(s.looking?1:0).Append(',')
                    .Append(s.playerX.ToString("F4",culture)).Append(',').Append(s.cameraX.ToString("F4",culture)).AppendLine();
            }
            File.WriteAllText(path,csv.ToString());
            File.WriteAllText(path+".done",Application.unityVersion+" editor="+Application.isEditor+" frames="+count+" duration="+(Time.realtimeSinceStartupAsDouble-started));
            Complete=true;
        }
        private void Release()
        {
            foreach(var recorder in recorders) recorder.Dispose(); recorders.Clear();
            if(player!=null) player.SetHorizontalInput(0);
            if(input!=null) input.enabled=inputWasEnabled;
            Application.runInBackground=background;
        }
        private void OnDisable() { if(capturing) {capturing=false; Release();} }
    }
}
#endif
