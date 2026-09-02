# 12-traits-response-lenient — done

## What changed

`Core/TraitsResponse.cs`:
- `Spots()` now filters candidates against `CatTraits.Allowed` itself (place,
  shade), drops repeated places, and caps at `CatTraits.MaxSpots` (2) —
  before constructing any `CatSpot`, so a bad mark never reaches the
  constructor and never throws. The base four traits stay strict/required,
  unchanged.
- New `IsNull()` helper: `String()` and `Strings()` now recognise the JSON
  literal `null` explicitly instead of scanning forward for the next quote
  or bracket, which used to land on the *next* key's name (`String`) or the
  *next* field's array (`Strings`, `white_markings:null` → `spots`'
  brackets).
- `CatTraits.cs` and `CatSpot.cs` were not touched — schema and
  `CatTraits.Allowed` unchanged, per SCOPE.

`Tests/Core/TraitsResponseTests.cs`: 5 new tests, no existing test changed.
- `AThirdSpotIsDroppedNotTheAnswer`
- `ARepeatedPlaceDropsTheSecondSpotNotTheAnswer`
- `AnUnknownPlaceDropsTheSpotNotTheAnswer`
- `ANullRequiredFieldIsAbsentNotTheNextKeysName`
- `ANullOptionalListDoesNotSwallowTheNextField`

## Test count

`dotnet test build/core-tests/core-tests.csproj`:
`Пройден!   : не пройдено     0, пройдено   266, пропущено     0, всего   266, длительность 492 ms. - core-tests.dll (net8.0)`

261 baseline + 5 new = 266. All green.

## Not done

Nothing outstanding from SCOPE. Not committed, per instruction.
