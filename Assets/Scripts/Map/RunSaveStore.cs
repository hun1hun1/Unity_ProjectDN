using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class RunSaveStore
{
    public string Path { get; }
    public RunSaveStore(string path) { Path=path; }
    public bool TryRead(out RunSaveData data,out string error)
    {
        data=null;error=null;
        try
        {
            if(File.Exists(Path+".ended") || !File.Exists(Path))return false;
            var file=new FileInfo(Path);
            if(file.Length>8*1024*1024)throw new InvalidDataException("Save is too large.");
            data=JsonUtility.FromJson<RunSaveData>(File.ReadAllText(Path,Encoding.UTF8));
            if(data==null || data.version!=1 || data.progress==null || data.inventory==null)
                throw new InvalidDataException("Unsupported or incomplete run save.");
            if(!data.hasRewards)data.rewards=null;
            return true;
        }
        catch(Exception exception) when(exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {data=null;error=exception.Message;return false;}
    }
    public bool TryWrite(RunSaveData data,out string error)
    {
        error=null;string temporary=Path+".tmp";
        try
        {
            string directory=System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
            Directory.CreateDirectory(directory);
            string json=JsonUtility.ToJson(data);
            using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                byte[] bytes=Encoding.UTF8.GetBytes(json);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
            }
            if(File.Exists(Path))File.Replace(temporary,Path,null);
            else File.Move(temporary,Path);
            if(File.Exists(Path+".ended"))File.Delete(Path+".ended");
            return true;
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {error=exception.Message;return false;}
        finally
        {
            try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    public bool TryDelete(out string error)
    {
        error=null;
        try
        {
            if(!File.Exists(Path))return true;
            // Invalidation is durable even if the old checkpoint is still locked for reading.
            using(var marker=new FileStream(Path+".ended",FileMode.Create,FileAccess.Write,FileShare.Read))
            {marker.WriteByte(1);marker.Flush(true);}
            try{File.Delete(Path);}catch(IOException){}catch(UnauthorizedAccessException){}
            return true;
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {error=exception.Message;return false;}
    }
}

public static class RunSaves
{
    private static RunSaveStore store;
    public static RunSaveStore Store => store ?? (store=new RunSaveStore(
        Environment.GetEnvironmentVariable("PROJECTDN_RUN_SAVE_PATH") ?? System.IO.Path.Combine(Application.persistentDataPath,"run-checkpoint.json")));
    public static void UseStore(RunSaveStore replacement) { store=replacement; }
    public static bool TryLoad(out RunSaveData data,out string error)
    {
        if(!Store.TryRead(out data,out error))return false;
        try{RunSaveCodec.Validate(data,RunAssetCatalog.Load());return true;}
        catch(Exception exception) when(exception is InvalidOperationException || exception is ArgumentException || exception is NullReferenceException)
        {data=null;error=exception.Message;return false;}
    }
    public static bool HasContinue => TryLoad(out _,out _);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { store=null; }
}
