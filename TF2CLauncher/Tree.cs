using System;
using System.Collections.Generic;
using System.IO;

namespace TF2CLauncher
{
    public class Tree
    {
        private List<Patch> tree;
        private String repo;

        public Tree(String repo)
        {
            this.repo = repo;
            refresh();
        }

        public void refresh()
        {
            tree = new List<Patch>();
            String line;
            StreamReader sr = new StreamReader("tree.txt");
            int prevVersion = -1;
            while ((line = sr.ReadLine()) != null)
            {
                String[] splitLine = line.Split(';');
                int version = Int32.Parse(splitLine[0]);
                int parentVersion = splitLine[4] == "" ? -1 : Int32.Parse(splitLine[4]);
                tree.Add(new Patch(this, prevVersion, version, splitLine[1], splitLine[2], splitLine[3], parentVersion));
                prevVersion = version;
            }
            sr.Close();
        }

        public void printPatchInfo()
        {
            foreach (Patch p in tree)
            {
                Console.WriteLine(p.getInfo());
            }
        }

        public Patch getNextPatchFromVersion(int version)
        {
            int i = 0;
            while (i < tree.Count && tree[i].getPrevVersion() != version)
            {
                i++;
            }
            if (i < tree.Count)
                return tree[i];

            return null;
        }

        public int getLatestVersionNumber()
        {
            Patch latest = tree[0];
            foreach (Patch p in tree)
            {
                if (p.getVersion() > latest.getVersion())
                {
                    latest = p;
                }
            }

            return latest.getVersion();
        }

        public int getEarliestVersionNumber()
        {
            Patch earliest = tree[0];
            foreach (Patch p in tree)
            {
                if (p.getVersion() < earliest.getVersion())
                {
                    earliest = p;
                }
            }

            return earliest.getVersion();
        }

        public List<Patch> getTree()
        {
            return tree;
        }

        public String getRepo()
        {
            return repo;
        }
    }
}
