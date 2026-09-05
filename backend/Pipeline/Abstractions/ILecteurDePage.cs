using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Localise et lit le texte présent sur une page. C'est la première étape du
    /// pipeline de traduction : elle transforme une image en une liste de zones,
    /// chacune sachant où elle se trouve et ce qu'elle dit.
    /// </summary>
    /// <remarks>
    /// Localisation et lecture sont volontairement réunies dans une seule opération :
    /// les moteurs existants (PaddleOCR, Azure, Tesseract) font les deux en une passe,
    /// et les séparer obligerait à faire le travail deux fois.
    /// <para>
    /// Une implémentation qui combine deux technologies — par exemple un détecteur
    /// spécialisé manga pour la position, puis un moteur généraliste pour la lecture —
    /// reste une seule implémentation de ce contrat. La combinaison est un détail
    /// interne, elle ne remonte pas jusqu'ici.
    /// </para>
    /// <para>
    /// Le contrat impose <see cref="IDisposable"/> parce que celui qui l'utilise ne
    /// peut pas savoir ce qu'il y a derrière : un moteur local retient ses modèles en
    /// mémoire native, qu'aucun ramasse-miettes ne libérera, là où un lecteur distant
    /// ne retient rien. Une implémentation sans ressource écrit un <c>Dispose</c>
    /// vide, ce qui ne coûte rien — c'est le choix que fait déjà .NET pour
    /// <see cref="System.Collections.Generic.IEnumerator{T}"/>.
    /// </para>
    /// <para>
    /// Charger les modèles prend plusieurs secondes : construire un lecteur par page
    /// serait un gâchis. On en construit un pour tout un chapitre, et on le libère à
    /// la fin.
    /// </para>
    /// </remarks>
    public interface ILecteurDePage : IDisposable
    {
        /// <summary>
        /// Analyse une page et en extrait toutes les zones de texte trouvées.
        /// </summary>
        /// <param name="image">
        /// Contenu binaire du fichier image, tel quel et non décodé. Le format
        /// (JPEG, PNG, WebP) est reconnu à partir des premiers octets : il n'a pas
        /// à être précisé.
        /// </param>
        /// <param name="jetonAnnulation">Jeton permettant d'interrompre le traitement.</param>
        /// <returns>
        /// Les zones trouvées, chacune portant sa géométrie, son texte et la confiance
        /// du moteur. La liste est vide si la page ne contient aucun texte lisible.
        /// L'ordre de lecture n'est pas déterminé à ce stade : les zones arrivent dans
        /// l'ordre où le moteur les a rencontrées.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="image"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si <paramref name="image"/> est vide ou ne contient pas une image décodable.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Levée si le traitement est interrompu via <paramref name="jetonAnnulation"/>.
        /// </exception>
        Task<IReadOnlyList<ZoneDeTexte>> LireAsync(byte[] image, CancellationToken jetonAnnulation = default);
    }
}
