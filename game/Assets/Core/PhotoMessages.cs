using System;

namespace CatShelter.Core
{
    /// <summary>
    /// 50-photo/06 VERIFY item 1: which Copy.cs key each outcome maps to.
    /// Pure data, so it lives in Core where dotnet test can guard the
    /// totality; Shell.PhotoMessages keeps only the Copy.Of lookup, which
    /// needs the engine's string table.
    /// </summary>
    public static class PhotoMessageKey
    {
        public const string NoAnimal = "photo.no_animal";
        public const string Dog = "photo.dog";
        public const string UnclearCat = "photo.unclear";
        public const string Cat = "photo.accepted";

        public static string For(PhotoOutcome outcome)
        {
            switch (outcome)
            {
                case PhotoOutcome.NoAnimal: return NoAnimal;
                case PhotoOutcome.Dog: return Dog;
                case PhotoOutcome.UnclearCat: return UnclearCat;
                case PhotoOutcome.Cat: return Cat;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }
    }
}
