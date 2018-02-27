using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSystem
{
    class Program
    {
        static void Main(string[] args)
        {
            //Create a packed file from a subtree
            if(!VFSPack.writeVFSPack("pack00.pac", "data/")) {
                return;
            }

            //Mount 
            FileSystem fileSystem = new FileSystem();
            fileSystem.mountOS("data/");
            fileSystem.mountPack("pack00.pac");

            IDirectory directory0 = fileSystem.openDirectory(string.Empty);
            IDirectory directory1 = fileSystem.openDirectory("data");
            IFile file0 = fileSystem.openFile("data/test00.txt");
            IFile file1 = directory1.openFile("test00.txt");

            int size = file0.Size;
            byte[] buffer = new byte[size];
            file0.read(0, size, buffer);

            fileSystem.closeFile(file1);
            fileSystem.closeFile(file0);
            fileSystem.closeDirectory(directory1);
            fileSystem.closeDirectory(directory0);

            fileSystem.unmount(0);
            fileSystem.unmount(1);
        }
    }
}
