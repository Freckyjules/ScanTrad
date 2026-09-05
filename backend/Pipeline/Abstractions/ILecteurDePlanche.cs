using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Localise et lit le texte présent sur une planche. Première étape du pipeline :
    /// elle transforme une image en une liste de zones, chacune sachant où elle se
    /// trouve et ce qu'elle dit.
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
    /// Charger les modèles prend plusieurs secondes : construire un lecteur par
    /// planche serait un gâchis. On en construit un pour tout un chapitre, et on le
    /// libère à la fin.
    /// </para>
    /// </remarks>
    public interface ILecteurDePlanche : IDisposable
    {
        /// <summary>
        /// Analyse une planche et en extrait toutes les zones de texte trouvées.
        /// </summary>
        /// <param name="planche">
        /// La planche à lire. Seule son image est utilisée ; les zones qu'elle porte
        /// déjà sont remplacées.
        /// </param>
        /// <param name="jetonAnnulation">Jeton permettant d'interrompre le traitement.</param>
        /// <returns>
        /// Une nouvelle planche portant les zones trouvées, chacune avec sa géométrie,
        /// son texte et la confiance du moteur. La liste est vide si la planche ne
        /// contient aucun texte lisible.
        /// <para>
        /// La sortie est brute : ni regroupement en bulles, ni ordre de lecture. Ce
        /// sont des calculs sur l'ensemble de la planche, qui demandent une
        /// connaissance du manga qu'un moteur d'OCR n'a pas.
        /// </para>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si l'image de la planche est vide ou n'est pas décodable.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Levée si le traitement est interrompu via <paramref name="jetonAnnulation"/>.
        /// </exception>
        Task<Planche> LireAsync(Planche planche, CancellationToken jetonAnnulation = default);
    }
}
