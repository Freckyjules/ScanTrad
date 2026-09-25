namespace ScanTrad.Application.Common
{
    /// <summary>
    /// Famille d'une erreur métier, indépendante de tout protocole : c'est la couche
    /// qui expose l'application (l'API web, par exemple) qui la traduit dans ses
    /// propres termes.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// Une règle n'est pas respectée par les données reçues.
        /// </summary>
        Validation,

        /// <summary>
        /// La ressource à créer existe déjà.
        /// </summary>
        Conflict,

        /// <summary>
        /// La ressource demandée n'existe pas.
        /// </summary>
        NotFound
    }
}
