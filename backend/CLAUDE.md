# Conventions de code C#

Ce dossier ne contient que du C#. Ces règles s'appliquent à **toutes les classes écrites ici** (entités, managers, etc.).

1. **Propriétés classiques, pas d'auto-property** : écrire le `get`/`set` explicitement avec un corps (`get { return champ; }` / `set { champ = value; }`), jamais `{ get; set; }`.
2. **Nommage** : le champ privé (l'attribut) en `camelCase` **minuscule**, la propriété publique en `PascalCase` **majuscule**.
3. **Initialisation dans le constructeur** : déclarer les champs nus (`private string name;`) et les initialiser dans le **constructeur**, jamais à la déclaration.
4. **Documentation XML** : documenter avec `/// <summary>` **uniquement les membres `public`** (classe, constructeur, propriétés, méthodes) — **pas** les champs `private`, sauf s'il s'agit d'un privé « technique » (non trivial). Pour les méthodes, mettre tout ce qu'il faut : `<summary>`, un `<param>` par paramètre (entrées), `<returns>` (sortie) et un `<exception>` par exception possible. **Toute la documentation (les `summary`) est rédigée en français.**

Exemple de référence (illustre les 4 règles) :

```csharp
/// <summary>
/// Représente un artiste.
/// </summary>
public class Artist
{
    private string name;

    /// <summary>
    /// Initialise un nouvel artiste avec des valeurs par défaut.
    /// </summary>
    public Artist()
    {
        name = string.Empty;
    }

    /// <summary>
    /// Nom de l'artiste.
    /// </summary>
    public string Name
    {
        get { return name; }
        set { name = value; }
    }
}
```
