using System.Runtime.InteropServices;

using Xunit.Extensions.AssemblyFixture;

using Xunit;

// Dans les projets de type SDK comme celui-là, plusieurs attributs d'assembly définis
// historiquement dans ce fichier sont maintenant automatiquement ajoutés pendant
// la génération et renseignés avec des valeurs définies dans les propriétés du projet.
// Pour plus d'informations sur les attributs à inclure et sur la personnalisation
// de ce processus, consultez : https://aka.ms/assembly-info-properties


// La définition de ComVisible sur False rend les types dans cet assembly invisibles
// aux composants COM. Si vous devez accéder à un type dans cet assembly à partir
// de COM, définissez l'attribut ComVisible sur True pour ce type.

[assembly: ComVisible(false)]

// Le GUID suivant concerne l'ID de typelib si ce projet est exposé à COM.

[assembly: Guid("1cea1885-54c9-4b2f-8dd2-5e7fd9955c75")]
[assembly: TestFramework(AssemblyFixtureFramework.TypeName, AssemblyFixtureFramework.AssemblyName)]
