using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RankSSpawnHelper.Models
{
    public class SpawnPoints
    {
        public float x;
        public float y;
        public string key;
        public bool verified;
        public string reporter;
    }

    public class HuntMap
    {
        public List<SpawnPoints> spawnPoints;
    }
}
