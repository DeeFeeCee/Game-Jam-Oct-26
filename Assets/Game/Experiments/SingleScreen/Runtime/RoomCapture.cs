using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameJam.SingleScreen
{
    /// <summary>Explicit evidence capture; never runs during ordinary play.</summary>
    public static class RoomCapture
    {
        [Serializable] public class Metrics
        {
            public int width,height,pixelRatio;
            public float orthographicSize;
            public Rect viewport;
            public Vector3 lowerLeft,upperRight,cameraPosition;
            public Vector2 playerSpriteWorldSize;
        }
        public static Metrics Save(Camera camera,int width,int height,string path,SpriteRenderer player)
        {
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var target=new RenderTexture(width,height,24){filterMode=FilterMode.Point};
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                var pp=camera.GetComponent<PixelPerfectCamera>();
                var result=new Metrics{width=width,height=height,pixelRatio=pp.pixelRatio,orthographicSize=camera.orthographicSize,
                    viewport=camera.pixelRect,lowerLeft=camera.ViewportToWorldPoint(new Vector3(0,0,10)),
                    upperRight=camera.ViewportToWorldPoint(new Vector3(1,1,10)),cameraPosition=camera.transform.position,
                    playerSpriteWorldSize=player!=null?(Vector2)player.bounds.size:Vector2.zero};
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());
                File.WriteAllText(path+".json",JsonUtility.ToJson(result,true));return result;
            }
            finally
            {
                camera.targetTexture=previousTarget;RenderTexture.active=previousActive;
                target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);
            }
        }
    }
}
