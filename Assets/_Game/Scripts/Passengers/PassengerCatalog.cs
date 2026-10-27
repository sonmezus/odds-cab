using System;
using System.Collections.Generic;
using UnityEngine;
using OddsCab.Puzzles;

namespace OddsCab
{
    public enum PassengerId
    {
        Adventurer = 0,
        Grandfather = 1,
        Chef = 2,
        Minstrel = 3,
        Professional = 4
    }

    [Serializable]
    public class PassengerProfile
    {
        public PassengerId id;
        public string resourceModelName;
        public string resourceModelNameV2;
        public string resourceFaceModelName;
        public string eModelPropName;
        public string nameTR;
        public string nameEN;
        public Color accentColor;
        public PuzzleCategory preferredCategory;

        public string DisplayName => Loc.Pick(nameTR, nameEN);
    }

    public static class PassengerCatalog
    {
        public static readonly PassengerProfile[] All =
        {
            new PassengerProfile
            {
                id = PassengerId.Adventurer,
                resourceModelName = "Models/Passenger_Adventurer",
                resourceModelNameV2 = "ModelsV2/Öğrenci_Genç_Kız_Yolcu_base_basic_pbr",
                resourceFaceModelName = "ModelKYüzveVücutKemikli/Adventurer/base_basic_pbr",
                eModelPropName = "",
                nameTR = "Gezgin Defne",
                nameEN = "Defne the Adventurer",
                accentColor = new Color(0.91f, 0.45f, 0.36f),
                preferredCategory = PuzzleCategory.Gozlem
            },
            new PassengerProfile
            {
                id = PassengerId.Grandfather,
                resourceModelName = "Models/Passenger_Grandfather",
                resourceModelNameV2 = "ModelsV2/Dede_Yolcu_base_basic_pbr",
                resourceFaceModelName = "ModelKYüzveVücutKemikli/Grandfather/base_basic_pbr",
                eModelPropName = "Yolcu_Dede_Baston",
                nameTR = "Zarif Dede Çınar",
                nameEN = "Grandpa Cinar",
                accentColor = new Color(0.30f, 0.47f, 0.68f),
                preferredCategory = PuzzleCategory.Bilmece
            },
            new PassengerProfile
            {
                id = PassengerId.Chef,
                resourceModelName = "Models/Passenger_Chef",
                resourceModelNameV2 = "ModelsV2/Aşçı_Kadın_Yolcu_base_basic_pbr",
                resourceFaceModelName = "ModelKYüzveVücutKemikli/Chef/base_basic_pbr",
                eModelPropName = "Aşçı_Kadın_Kruvasan_Pasta_Kutusu",
                nameTR = "Küçük Şef Ava",
                nameEN = "Little Chef Ava",
                accentColor = new Color(0.36f, 0.72f, 0.64f),
                preferredCategory = PuzzleCategory.Hesap
            },
            new PassengerProfile
            {
                id = PassengerId.Minstrel,
                resourceModelName = "Models/Passenger_Minstrel",
                resourceModelNameV2 = "ModelsV2/Müzisyen_Genç_Yolcu_base_basic_pbr",
                resourceFaceModelName = "ModelKYüzveVücutKemikli/Minstrel/base_basic_pbr",
                eModelPropName = "Müzisyen_Genç_Çantası_Ukulele_Kılıfı",
                nameTR = "Gezgin Ozan",
                nameEN = "Wandering Minstrel",
                accentColor = new Color(0.82f, 0.52f, 0.25f),
                preferredCategory = PuzzleCategory.Kelime
            },
            new PassengerProfile
            {
                id = PassengerId.Professional,
                resourceModelName = "Models/Passenger_Professional",
                resourceModelNameV2 = "ModelsV2/Memur_Kadın_Yolcu_base_basic_pbr",
                resourceFaceModelName = "ModelKYüzveVücutKemikli/Professional/base_basic_pbr",
                eModelPropName = "Memur_Kadın_Yolcu_Evrak_Çantası",
                nameTR = "Avukat Selin",
                nameEN = "Selin the Professional",
                accentColor = new Color(0.28f, 0.35f, 0.52f),
                preferredCategory = PuzzleCategory.Cikarim
            },
        };

        public static PassengerProfile Get(PassengerId id) => All[(int)id % All.Length];

        public static PassengerProfile ForStop(int stop)
        {
            int idx = ((stop - 1) * 3 + (stop - 1) / 5) % All.Length;
            return All[idx];
        }
    }
}
