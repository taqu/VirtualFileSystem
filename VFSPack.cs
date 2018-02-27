
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileSystem
{
    //----------------------------------------------
    //---
    //--- Entry
    //---
    //----------------------------------------------
    internal class Entry
    {
        public enum EType
        {
            File,
            Directory,
        };

        public Entry(EType type, string path, string name, int size)
        {
            type_ = type;
            if(EType.File == type_) {
                flags_ = new System.Collections.Specialized.BitVector32();
                name_ = name;
                path_ = path;
                size_ = size;
                numChildren_ = 0;
            } else {
                flags_ = new System.Collections.Specialized.BitVector32();
                name_ = name;
                path_ = path;
                size_ = 0;
                numChildren_ = size;
            }
        }

        public EType Type {get { return type_;} }
        public string Name {get { return name_;} }
        public string Path {get { return path_;} }
        public int Size {get { return size_;} }
        public int NumChildren {get { return numChildren_;} set {numChildren_ = value; } }
        public bool checkFlag(int flag)
        {
            return flags_[flag];
        }

        public void setFlag(int flag, bool value)
        {
            flags_[flag] = value;
        }

        private EType type_;
        private System.Collections.Specialized.BitVector32 flags_;
        private string name_;
        private string path_;
        private int size_;
        private int numChildren_;
    };

    //----------------------------------------------
    //---
    //--- Traversal
    //---
    //----------------------------------------------
    internal class Traversal
    {
        /**
        @brief Combine parent's path and a name
        */
        private static bool createNextPath(out string path, string parentPath, string name)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(parentPath));
            System.Diagnostics.Debug.Assert(null != name);
            path = string.Empty;
            if(parentPath.Length<=0) {
                return false;
            }
            parentPath = parentPath.TrimEnd('*');
            path = parentPath;
            if(parentPath.Length<=0 || Static.Deliminator != path[parentPath.Length-1]) {
                path += Static.Deliminator;
            }
            path += name;
            return true;
        }

        /**
        */
        private static Entry createFile(string parentPath, string name, int size)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(parentPath));
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(name));
            System.Diagnostics.Debug.Assert(0<=size);

            string path = null;
            if(!createNextPath(out path, parentPath, name)) {
                return null;
            }
            Entry entry = new Entry(Entry.EType.File, path, name, size);
            return entry;
        }

        /**
        */
        private Entry createDirectory(string parentPath, string name, int numChildren)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(parentPath));
            System.Diagnostics.Debug.Assert(null != name);
            System.Diagnostics.Debug.Assert(0<=numChildren);

            string path = null;
            if(!createNextPath(out path, parentPath, name)) {
                return null;
            }
            Entry entry = new Entry(Entry.EType.Directory, path, name, numChildren);
            return entry;
        }

        /**
        */
        private static bool isNormalFile(FileAttributes attr)
        {
            return FileAttributes.Normal == (attr & FileAttributes.Normal)
                || FileAttributes.Archive == (attr & FileAttributes.Archive);
        }

        /**
        */
        private static bool isNormalDirectory(FileAttributes attr)
        {
            return 0 == (attr & ~FileAttributes.Directory);
        }

        /**
        @brief Count number of normal files
        */
        private static int countEnumerable(IEnumerable<FileInfo> enumerable)
        {
            int count = 0;
            IEnumerator<FileInfo> enumerator = enumerable.GetEnumerator();
            while(enumerator.MoveNext()) {
                if(isNormalFile(enumerator.Current.Attributes)) {
                    ++count;
                }
            }
            return count;
        }

        /**
        @brief Count number of normal directories
        */
        private static int countEnumerable(IEnumerable<DirectoryInfo> enumerable)
        {
            int count = 0;
            IEnumerator<DirectoryInfo> enumerator = enumerable.GetEnumerator();
            while(enumerator.MoveNext()) {
                if(isNormalDirectory(enumerator.Current.Attributes)) {
                    ++count;
                }
            }
            return count;
        }

        /**
        @brief Traverse and gather information of directories and files from a root point
        */
        public bool traverse(string root)
        {
            if(string.IsNullOrEmpty(root)) {
                return false;
            }
            try {
                FileAttributes attr = File.GetAttributes(root);

                Entry rootEntry = null;
                if(FileAttributes.Directory != (FileAttributes.Directory & attr)) {

                    FileInfo fileInfo = new FileInfo(root);
                    string filename = Path.GetFileName(root);
                    string dirpath = root.Substring(0, root.Length-filename.Length);
                    Entry fileEntry = createFile(dirpath, filename, (int)fileInfo.Length);
                    rootEntry = createDirectory("/", string.Empty, 1);

                    entries_.Clear();
                    entries_.Add(rootEntry);
                    entries_.Add(fileEntry);
                    return true;
                }

                DirectoryInfo rootInfo = new DirectoryInfo(root);
                string path = root;
                if(path.Length<=0) {
                    return false;
                }
                if(Static.Deliminator != path[path.Length-1]) {
                    path += "/*";
                }else {
                    path += "*";
                }
                entries_.Clear();
                rootEntry = createDirectory("/", string.Empty, 0);
                entries_.Add(rootEntry);

                int start = entries_.Count;
                DirectoryInfo[] directories = rootInfo.GetDirectories();
                for(int i=0; i<directories.Length; ++i) {
                    if(isNormalDirectory(directories[i].Attributes)){
                        int childCount = countEnumerable(directories[i].EnumerateFiles()) + countEnumerable(directories[i].EnumerateDirectories());
                        Entry entry = createDirectory(path, directories[i].Name, childCount);
                        entries_.Add(entry);
                    }
                }
                FileInfo[] files = rootInfo.GetFiles();
                for(int i=0; i<files.Length; ++i) {
                    if(isNormalFile(files[i].Attributes)) {
                        Entry entry = createFile(path, files[i].Name, (int)files[i].Length);
                        entries_.Add(entry);
                    }
                }

                int end = entries_.Count;
                rootEntry.NumChildren = end-start;
                for(int i=start; i<end; ++i) {
                    if(entries_[i].Type == Entry.EType.Directory) {
                        traverseDirectory(entries_[i]);
                    }
                }

            } catch {
                return false;
            }
            return true;
        }

        private bool traverseDirectory(Entry rootEntry)
        {
            try {
                int start = entries_.Count;
                DirectoryInfo rootInfo = new DirectoryInfo(rootEntry.Path);
                DirectoryInfo[] directories = rootInfo.GetDirectories();
                FileInfo[] files = rootInfo.GetFiles();

                for(int i=0; i<directories.Length; ++i) {
                    if(isNormalDirectory(directories[i].Attributes)) {
                        int childCount = countEnumerable(directories[i].EnumerateFiles()) + countEnumerable(directories[i].EnumerateDirectories());
                        Entry entry = createDirectory(rootEntry.Path, directories[i].Name, childCount);
                        entries_.Add(entry);
                    }
                }
                for(int i=0; i<files.Length; ++i) {
                    if(isNormalFile(files[i].Attributes)) {
                        Entry entry = createFile(rootEntry.Path, files[i].Name, (int)files[i].Length);
                        entries_.Add(entry);
                    }
                }
                int end = entries_.Count;
                rootEntry.NumChildren = end - start;
                for(int i=start; i<end; ++i) {
                    if(entries_[i].Type == Entry.EType.Directory) {
                        traverseDirectory(entries_[i]);
                    }
                }
                return true;
            } catch {
                return false;
            }
        }

        public bool write(string path)
        {
            try {
                StringBuilder stringBuffer = new StringBuilder();
                using(FileStream filestream = new FileStream(path, FileMode.Create))
                using(RNGCryptoServiceProvider criptoRandom = new RNGCryptoServiceProvider())
                using(MemoryStream dataBuffer = new MemoryStream())
                using(BinaryWriter dataWriter = new BinaryWriter(dataBuffer))
                using(StringWriter stringWriter = new StringWriter(stringBuffer))
                using(MemoryStream fileBuffer = new MemoryStream())
                using(BinaryWriter fileBinaryWriter = new BinaryWriter(fileBuffer)) {
                    VFSPack.Header header = new VFSPack.Header();
                    header.signature_ = VFSPack.VFSPackSignature;
                    byte[] rnd = new byte[4];
                    criptoRandom.GetBytes(rnd);
                    header.reserved_ = ((uint)rnd[3]<<24) | ((uint)rnd[2]<<16) | ((uint)rnd[1]<<8) | (uint)rnd[0];

                    header.numEntries_ = entries_.Count;
                    header.offsetString_ = VFSPack.Header.Size + VFSPack.FileData.Size*entries_.Count;

                    //Create data entries
                    long dataOffset = 0;
                    int offsetChild = 1;
                    for(int i = 0; i<entries_.Count; ++i) {
                        if(entries_[i].Type == Entry.EType.File) {
                            VFSPack.FileData fileData = new VFSPack.FileData();
                            fileData.type_ = (int)Type.File;
                            fileData.flags_ = 0;
                            fileData.nameOffset_ = stringBuffer.Length;
                            fileData.nameLength_ = entries_[i].Name.Length;
                            fileData.dataOffset_ = dataOffset;
                            fileData.dataSize_ = entries_[i].Size;
                            dataOffset += entries_[i].Size;
                            fileData.serialize(dataWriter);
                        } else {
                            VFSPack.FileData directoryData = new VFSPack.FileData();
                            directoryData.type_ = (int)Type.Directory;
                            directoryData.flags_ = 0;
                            directoryData.nameOffset_ = stringBuffer.Length;
                            directoryData.nameLength_ = entries_[i].Name.Length;
                            directoryData.dataOffset_ = offsetChild;
                            directoryData.dataSize_ = entries_[i].NumChildren;
                            offsetChild += entries_[i].NumChildren;
                            directoryData.serialize(dataWriter);
                        }
                        dataWriter.Flush();

                        stringWriter.Write(entries_[i].Name);
                        stringWriter.Flush();
                    }

                    VFSPack.Adler32 adler32 = new VFSPack.Adler32();
                    adler32.initialize();

                    byte[] dataBytes = dataBuffer.ToArray();
                    string str = stringBuffer.ToString();
                    byte[] strBytes = Encoding.Unicode.GetBytes(str);
                    header.offsetData_ = header.offsetString_ + strBytes.Length;
                    header.serialize(fileBinaryWriter);
                    adler32.update(fileBuffer.ToArray());

                    fileBinaryWriter.Write(dataBytes);
                    adler32.update(dataBytes);

                    fileBinaryWriter.Write(strBytes);
                    adler32.update(strBytes);

                    for(int i = 0; i<entries_.Count; ++i) {
                        if(entries_[i].Type != Entry.EType.File) {
                            continue;
                        }
                        byte[] bytes = File.ReadAllBytes(entries_[i].Path);
                        fileBinaryWriter.Write(bytes, 0, bytes.Length);
                        adler32.update(bytes);
                    }

                    VFSPack.Footer footer = new VFSPack.Footer();
                    footer.checksum_ = adler32.finalize();
                    footer.serialize(fileBinaryWriter);

                    byte[] finalBytes = fileBuffer.ToArray();
                    filestream.Write(finalBytes, 0, finalBytes.Length);

                    fileBinaryWriter.Close();
                    fileBuffer.Close();
                    stringWriter.Close();
                    dataWriter.Close();
                    dataBuffer.Close();
                    filestream.Close();
                }
                return true;

            } catch {
                return false;
            }
        }

        private List<Entry> entries_ = new List<Entry>();
    };

    //----------------------------------------------
    //---
    //--- VFSPack
    //---
    //----------------------------------------------
    public static class VFSPack
    {
        public const uint VFSPackSignature = 0x4B434150;

        public struct Header
        {
            public const int Size = 4*2 + 8*3;
            public uint signature_;
            public uint reserved_;
            public long numEntries_;
            public long offsetString_;
            public long offsetData_;

            public void serialize(BinaryWriter binaryWriter)
            {
                binaryWriter.Write(signature_);
                binaryWriter.Write(reserved_);
                binaryWriter.Write(numEntries_);
                binaryWriter.Write(offsetString_);
                binaryWriter.Write(offsetData_);
            }

            public void deserialize(BinaryReader binaryReader)
            {
                signature_ = binaryReader.ReadUInt32();
                reserved_ = binaryReader.ReadUInt32();
                numEntries_ = binaryReader.ReadInt64();
                offsetString_ = binaryReader.ReadInt64();
                offsetData_ = binaryReader.ReadInt64();
            }
        };

        public struct FileData
        {
            public const int Size = 4*5 + 8;
            public int type_;
            public int flags_;
            public int nameOffset_;
            public int nameLength_;
            public int dataSize_;
            public long dataOffset_;

            public void serialize(BinaryWriter binaryWriter)
            {
                binaryWriter.Write(type_);
                binaryWriter.Write(flags_);
                binaryWriter.Write(nameOffset_);
                binaryWriter.Write(nameLength_);
                binaryWriter.Write(dataSize_);
                binaryWriter.Write(dataOffset_);
            }

            public void deserialize(BinaryReader binaryReader)
            {
                type_ = binaryReader.ReadInt32();
                flags_ = binaryReader.ReadInt32();
                nameOffset_ = binaryReader.ReadInt32();
                nameLength_ = binaryReader.ReadInt32();
                dataSize_ = binaryReader.ReadInt32();
                dataOffset_ = binaryReader.ReadInt64();
            }

            public int FileSize { get { return dataSize_; } }
            public int NumChildren { get { return dataSize_; } }
            public long DataOffset { get { return dataOffset_; } }
            public long ChildrenOffset { get { return dataOffset_; } }
        };

        public struct Footer
        {
            public const int Size = 4;
            public uint checksum_;

            public void serialize(BinaryWriter binaryWriter)
            {
                binaryWriter.Write(checksum_);
            }

            public void serialize(BinaryReader binaryReader)
            {
                checksum_ = binaryReader.ReadUInt32();
            }
        };

        public struct Adler32
        {
            public const int MOD_ADLER = 65521;

            public void initialize()
            {
                a_ = 1;
                b_ = 0;
            }


            public void update(byte[] bytes)
            {
                int len = bytes.Length;
                int count = 0;
                while(0<len) {
                    int t = (5550<len) ? 5550 : len;
                    len -= t;
                    do {
                        a_ += bytes[count];
                        b_ += a_;
                        ++count;
                        --t;
                    } while(0<t);
                    a_ %= MOD_ADLER;
                    b_ %= MOD_ADLER;
                }
            }

            public uint finalize()
            {
                return (b_<<16) | a_;
            }

            private uint a_;
            private uint b_;
        };

        public static bool writeVFSPack(string path, string root)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(path));
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(root));
            Traversal traversal = new Traversal();
            if(!traversal.traverse(root)) {
                return false;
            }
            return traversal.write(path);
        }

        public struct Pack : System.IDisposable
        {
            public FileStream file_;
            public BinaryReader binaryReader_;
            public long numEntries_;
            public FileData[] entries_;
            public string stringTable_;

            public void Dispose()
            {
                stringTable_ = null;

                entries_ = null;
                numEntries_ = 0;

                if(null != binaryReader_) {
                    binaryReader_.Close();
                    binaryReader_.Dispose();
                    binaryReader_ = null;
                }
                if(null != file_) {
                    file_.Close();
                    file_.Dispose();
                    file_ = null;
                }
            }
        };

        public static bool readVFSPack(out Pack pack, string path, bool checkHash)
        {
            pack = new Pack();
            try {
                pack.file_ = new FileStream(path, FileMode.Open);
                pack.binaryReader_ = new BinaryReader(pack.file_);
                Header header = new Header();
                header.deserialize(pack.binaryReader_);
                if(VFSPackSignature != header.signature_) {
                    pack.Dispose();
                    return false;
                }
                if(header.numEntries_<0
                    || header.offsetString_<0
                    || header.offsetData_<0
                    || header.offsetData_<header.offsetString_) {
                    return false;
                }

                long entryTop = pack.file_.Position;

                //Read string table
                pack.file_.Position = header.offsetString_;
                int strNumBytes = (int)(header.offsetData_ - header.offsetString_);
                byte[] strBytes = new byte[strNumBytes];
                if(strNumBytes != pack.binaryReader_.Read(strBytes, 0, strNumBytes)) {
                    pack.Dispose();
                    return false;
                }
                pack.stringTable_ = Encoding.Unicode.GetString(strBytes);

                //Read entries
                pack.file_.Position = entryTop;
                pack.numEntries_ = header.numEntries_;
                pack.entries_ = new FileData[pack.numEntries_];
                for(long i = 0; i<pack.numEntries_; ++i) {
                    pack.entries_[i].deserialize(pack.binaryReader_);
                    if(pack.entries_[i].type_ == (int)Type.File) {
                        pack.entries_[i].dataOffset_ += header.offsetData_;
                    }
                }
                return true;
            } catch {
                return false;
            }
        }
    }
}
