using System;
using System.Collections.Generic;
using System.IO;

namespace FileSystem
{
    public static class Static
    {
        public const char Deliminator = '/';
    };

    public enum Type
    {
        File,
        Directory,
    };

    public enum VFSType
    {
        VFSType_Pack,
        VFSType_OS,
    };

    //--------------------------------------------
    //---
    //--- IFile
    //---
    //--------------------------------------------
    public interface IFile : IDisposable
    {
        int Size { get; }
        string Name { get; }
        int read(long offset, int size, byte[] buffer);
        void close();
    };

    //--------------------------------------------
    //---
    //--- IDirectory
    //---
    //--------------------------------------------
    public interface IDirectory : IDisposable
    {
        int NumDirectories { get; }
        int NumFiles { get; }
        string Name { get; }

        int findDirectory(string path, int start, int length);
        int findFile(string path, int start, int length);

        IFile openFile(int index);
        IFile openFile(string path, int start, int length);
        IFile openFile(string name);

        IDirectory openDirectory(int index);
        IDirectory openDirectory(string path, int start, int length);
        IDirectory openDirectory(string name);
        void close();
    };

    //--------------------------------------------
    //---
    //--- FilePack
    //---
    //--------------------------------------------
    public class FilePack : IFile
    {
        public FilePack(VirtualFileSystemPack parent, int id)
        {
            parent_ = parent;
            id_ = id;
        }

        public int Size { get { return parent_.getFileData(id_).dataSize_; } }
        public string Name { get { return parent_.getName(id_); } }

        public int read(long offset, int size, byte[] buffer)
        {
            try {
                FileStream fstream = parent_.getFileStream();
                fstream.Position = parent_.getFileData(id_).dataOffset_ + offset;
                return fstream.Read(buffer, 0, size);
            } catch {
                return -1;
            }
        }

        public void close()
        {
            Dispose();
        }

        public void Dispose()
        {
        }

        private VirtualFileSystemPack parent_;
        private int id_;
    };

    //--------------------------------------------
    //---
    //--- DirectoryPack
    //---
    //--------------------------------------------
    public class DirectoryPack : IDirectory
    {
        public DirectoryPack(VirtualFileSystemPack parent, int id)
        {
            parent_ = parent;
            id_ = id;

            numDirectories_ = 0;
            numFiles_ = 0;
            VFSPack.FileData fileData = parent_.getFileData(id_);
            int offset = (int)fileData.ChildrenOffset;
            for(int i=0; i<fileData.NumChildren; ++i) {
                int childId = offset + i;
                if(parent_.getFileData(childId).type_ == (int)Type.File) {
                    ++numFiles_;
                } else {
                    ++numDirectories_;
                }
            }
        }

        public int NumDirectories { get { return numDirectories_; } }
        public int NumFiles { get { return numFiles_; } }
        public string Name { get { return parent_.getName(id_); } }

        public int findDirectory(string path, int start, int length)
        {
            int childId = parent_.findEntry(id_, path, start, length);
            if(childId<0) {
                return -1;
            }
            return (int)Type.Directory == parent_.getFileData(childId).type_? childId : -1;
        }

        public int findFile(string path, int start, int length)
        {
            int childId = parent_.findEntry(id_, path, start, length);
            if(childId<0) {
                return -1;
            }
            return (int)Type.File == parent_.getFileData(childId).type_? childId : -1;
        }

        public IDirectory openDirectory(int index)
        {
            int childId = (int)parent_.getFileData(id_).ChildrenOffset+index;
            return (int)Type.Directory == parent_.getFileData(childId).type_ ? new DirectoryPack(parent_, childId) : null;
        }

        public IDirectory openDirectory(string path, int start, int length)
        {
            int childId = parent_.findEntry(id_, path, start, length);
            if(childId<0) {
                return null;
            }
            return (int)Type.Directory == parent_.getFileData(childId).type_? new DirectoryPack(parent_, childId) : null;
        }

        public IDirectory openDirectory(string name)
        {
            return openDirectory(name, 0, name.Length);
        }

        public IFile openFile(int index)
        {
            int childId = (int)parent_.getFileData(id_).ChildrenOffset+index;
            return (int)Type.File == parent_.getFileData(childId).type_ ? new FilePack(parent_, childId) : null;
        }

        public IFile openFile(string path, int start, int length)
        {
            int childId = parent_.findEntry(id_, path, start, length);
            if(childId<0) {
                return null;
            }
            return (int)Type.File == parent_.getFileData(childId).type_? new FilePack(parent_, childId) : null;
        }

        public IFile openFile(string name)
        {
            return openFile(name, 0, name.Length);
        }

        public void close()
        {
            Dispose();
        }

        public void Dispose()
        {
        }


        private VirtualFileSystemPack parent_;
        private int id_;
        private int numDirectories_;
        private int numFiles_;

    };

    //--------------------------------------------
    //---
    //--- VirtualFileSystemPack
    //---
    //--------------------------------------------
    public class VirtualFileSystemPack : IVirtualFileSystem
    {
        public VirtualFileSystemPack(VFSPack.Pack pack)
        {
            pack_ = pack;
        }

        public VFSType FSType { get { return VFSType.VFSType_Pack; } }

        public void closeDirectory(IDirectory directory)
        {
            if(null == directory) {
                return;
            }
            directory.Dispose();
        }

        public void closeFile(IFile file)
        {
            if(null == file) {
                return;
            }
            file.Dispose();
        }

        public IDirectory openDirectory(string path)
        {
            System.Diagnostics.Debug.Assert(null != path);

            int pathStart = 0;
            if(0<path.Length) {
                if(path[0] == Static.Deliminator) {
                    ++pathStart;
                }
            }
            if(path.Length<=pathStart) {
                return new DirectoryPack(this, 0);
            }
            int nameStart;
            int nameLength;
            int id = 0;
            for(;;){
                getNextNameFromPath(out nameStart, out nameLength, pathStart, path);

                id = findEntry(id, path, nameStart, nameLength);
                if(id<0) {
                    return null;
                }
                VFSPack.FileData fileData = getFileData(id);
                if(fileData.type_ == (int)Type.File) {
                    return null;
                }
                pathStart += nameLength;
                if(path.Length<=pathStart) {
                    return new DirectoryPack(this, id);
                }
            }
        }

        public IFile openFile(string path)
        {
            System.Diagnostics.Debug.Assert(null != path);

            int start = 0;
            if(0<path.Length) {
                if(path[0] == Static.Deliminator) {
                    ++start;
                }
            }
            if(path.Length<=start) {
                return null;
            }
            int nameStart;
            int nameLength;
            int pathStart = 0;
            int id = 0;
            for(;;){
                getNextNameFromPath(out nameStart, out nameLength, pathStart, path);

                id = findEntry(id, path, nameStart, nameLength);
                if(id<0) {
                    return null;
                }
                VFSPack.FileData fileData = getFileData(id);

                pathStart += nameLength+1;
                if(path.Length<=pathStart) {
                    return ((int)Type.File == fileData.type_) ? new FilePack(this, id) : null;
                } else {
                    if((int)Type.Directory != fileData.type_) {
                        return null;
                    }
                }
            }
        }

        public IDirectory openRoot()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            pack_.Dispose();
        }

        public FileStream getFileStream()
        {
            return pack_.file_;
        }
        public VFSPack.FileData getFileData(int id)
        {
            return pack_.entries_[id];
        }

        public int compareName(int id, string path, int start, int length)
        {
            int nameOffset = pack_.entries_[id].nameOffset_;
            int nameLength = pack_.entries_[id].nameLength_;
            if(length != nameLength) {
                return -1;
            }
            for(int i=0; i<length; ++i) {
                if(pack_.stringTable_[nameOffset] != path[start]){
                    return -1;
                }
                ++nameOffset;
                ++start;
            }
            return 0;
        }

        public static void getNextNameFromPath(out int nameStart, out int nameLength, int pathStart, string path)
        {
            nameStart = pathStart;
            nameLength = 0;
            for(int i=pathStart; i<path.Length; ++i) {
                if(Static.Deliminator == path[i]) {
                    break;
                }
                ++nameLength;
            }
        }

        /**
        @return id of a found entry
        */
        public int findEntry(int id, string path, int start, int length)
        {
            VFSPack.FileData fileData = getFileData(id);
            int childId = (int)fileData.ChildrenOffset;
            for(int i = 0; i<fileData.NumChildren; ++i) {
                if(0 == compareName(childId, path, start, length)) {
                    return childId;
                }
                ++childId;
            }
            return -1;
        }

        public string getName(int id)
        {
            int nameOffset = pack_.entries_[id].nameOffset_;
            int nameLength = pack_.entries_[id].nameLength_;
            return pack_.stringTable_.Substring(nameOffset, nameLength);
        }

        private VFSPack.Pack pack_;
    };


    //--------------------------------------------
    //---
    //--- FileOS
    //---
    //--------------------------------------------
    public class FileOS : IFile
    {
        public FileOS(IVirtualFileSystem parent, FileInfo fileInfo)
        {
            parent_ = parent;
            fileInfo_ = fileInfo;
            fileStream_ = fileInfo_.Open(FileMode.Open);
        }

        public int Size {get { return (int)fileInfo_.Length;} }
        public string Name { get { return fileInfo_.Name; } }

        public int read(long offset, int size, byte[] buffer)
        {
            fileStream_.Position = offset;
            return fileStream_.Read(buffer, 0, size);
        }

        public void Dispose()
        {
            fileInfo_ = null;
            if(null != fileStream_) {
                fileStream_.Close();
                fileStream_.Dispose();
                fileStream_ = null;
            }
        }

        public void close()
        {
            Dispose();
        }

        private IVirtualFileSystem parent_;
        private FileInfo fileInfo_;
        private FileStream fileStream_;
    };

    //--------------------------------------------
    //---
    //--- DirectoryOS
    //---
    //--------------------------------------------
    public class DirectoryOS : IDirectory
    {
        public DirectoryOS(IVirtualFileSystem parent, DirectoryInfo directoryInfo)
        {
            parent_ = parent;
            directoryInfo_ = directoryInfo;
            directories_ = directoryInfo_.GetDirectories();
            files_ = directoryInfo_.GetFiles();
        }

        public int NumDirectories
        {
            get { return directories_.Length;}
        }
        public int NumFiles
        {
            get{ return files_.Length;}
        }

        public string Name{ get { return directoryInfo_.Name; } }

        public int findDirectory(string path, int start, int length)
        {
            for(int i=0; i<directories_.Length; ++i) {
                if(0 == string.Compare(directories_[i].Name, 0, path, start, length)) {
                    return i;
                }
            }
            return -1;
        }

        public int findFile(string path, int start, int length)
        {
            for(int i=0; i<files_.Length; ++i) {
                if(0 == string.Compare(files_[i].Name, 0, path, start, length)) {
                    return i;
                }
            }
            return -1;
        }

        public IDirectory openDirectory(int index)
        {
            return new DirectoryOS(parent_, directories_[index]);
        }

        public IDirectory openDirectory(string path, int start, int length)
        {
            int index = findDirectory(path, start, length);
            return (0<=index)? openDirectory(index) : null;
        }

        public IDirectory openDirectory(string name)
        {
            return openDirectory(name, 0, name.Length);
        }

        public IFile openFile(int index)
        {
            return new FileOS(parent_, files_[index]);
        }

        public IFile openFile(string path, int start, int length)
        {
            int index = findFile(path, start, length);
            return (0<=index)? openFile(index) : null;
        }

        public IFile openFile(string name)
        {
            return openFile(name, 0, name.Length);
        }

        public void close()
        {
            Dispose();
        }

        public void Dispose()
        {
            directoryInfo_ = null;
            files_ = null;
        }

        private IVirtualFileSystem parent_;
        private DirectoryInfo directoryInfo_;
        private DirectoryInfo[] directories_;
        private FileInfo[] files_;
    };

    //--------------------------------------------
    //---
    //--- IVirtualFileSystem
    //---
    //--------------------------------------------
    public interface IVirtualFileSystem : IDisposable
    {
        VFSType FSType {get; }
        IDirectory openRoot();
        IFile openFile(string path);
        void closeFile(IFile file);

        IDirectory openDirectory(string path);
        void closeDirectory(IDirectory directory);
    };

    //--------------------------------------------
    //---
    //--- VirtualFileSystemOS
    //---
    //--------------------------------------------
    public class VirtualFileSystemOS : IVirtualFileSystem
    {
        public VirtualFileSystemOS(string root)
        {
            System.Diagnostics.Debug.Assert(null != root);
            root_ = root;
            if(0<root_.Length) {
                if(Static.Deliminator != root_[root_.Length-1]) {
                    root_ += Static.Deliminator;
                }
            }
        }

        public VFSType FSType { get { return VFSType.VFSType_OS; } }

        public IDirectory openRoot()
        {
            try {
                return new DirectoryOS(this, new DirectoryInfo(root_));
            } catch {
                return null;
            }
        }

        public IFile openFile(string path)
        {
            stringBuilder_.Length = 0;
            stringBuilder_.Append(root_);
            stringBuilder_.Append(path);
            path = stringBuilder_.ToString();
            if(!System.IO.File.Exists(path)) {
                return null;
            }
            try {
                return new FileOS(this, new FileInfo(path));
            } catch {
                return null;
            }
        }

        public void closeFile(IFile file)
        {
            if(null == file) {
                return;
            }
            file.Dispose();
        }

        public IDirectory openDirectory(string path)
        {
            stringBuilder_.Length = 0;
            stringBuilder_.Append(root_);
            stringBuilder_.Append(path);
            path = stringBuilder_.ToString();
            if(!System.IO.Directory.Exists(path)) {
                return null;
            }
            try {
                return new DirectoryOS(this, new DirectoryInfo(path));
            } catch {
                return null;
            }
        }

        public void closeDirectory(IDirectory directory)
        {
            if(null == directory) {
                return;
            }
            directory.Dispose();
        }

        public void Dispose()
        {
        }

        private string root_;
        private System.Text.StringBuilder stringBuilder_ = new System.Text.StringBuilder(128);
    };

    //--------------------------------------------
    //---
    //--- FileSystem
    //---
    //--------------------------------------------
    public class FileSystem
    {
        public FileSystem()
        {
        }

        /**
        */
        public bool mountOS(string path)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(path));

            if(!System.IO.Directory.Exists(path)) {
                return false;
            }
            vfs_.Add(new VirtualFileSystemOS(path));
            return true;
        }

        /**
        */
        public bool mountPack(string path)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(path));

            VFSPack.Pack pack;
            if(!VFSPack.readVFSPack(out pack, path, false)) {
                return false;
            }
            vfs_.Add(new VirtualFileSystemPack(pack));
            return true;
        }

        /**
        */
        public void unmount(int index)
        {
            System.Diagnostics.Debug.Assert(0<=index && index<vfs_.Count);
            if(null != vfs_[index]) {
                vfs_[index].Dispose();
                vfs_[index] = null;
            }
            vfs_.RemoveAt(index);
        }

        /**
        @brief Search by descend order
        */
        public IFile openFile(string path)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(path));
            IFile file = null;
            for(int i=vfs_.Count-1; 0<=i; --i) {
                if(null != vfs_[i]) {
                    file = vfs_[i].openFile(path);
                    if(null != file) {
                        break;
                    }
                }
            }
            return file;
        }

        /**
        */
        public void closeFile(IFile file)
        {
            if(null == file) {
                return;
            }
            file.close();
        }

        /**
        @brief Search by descend order
        */
        public IDirectory openDirectory(string path)
        {
            System.Diagnostics.Debug.Assert(null != path);
            IDirectory directory = null;
            for(int i=vfs_.Count-1; 0<=i; --i) {
                if(null != vfs_[i]) {
                    directory = vfs_[i].openDirectory(path);
                    if(null != directory) {
                        break;
                    }
                }
            }
            return directory;
        }

        /**
        */
        public void closeDirectory(IDirectory directory)
        {
            if(null == directory) {
                return;
            }
            directory.close();
        }

        private List<IVirtualFileSystem> vfs_ = new List<IVirtualFileSystem>(8);
    }
}
