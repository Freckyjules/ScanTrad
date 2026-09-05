namespace ScanTrad.Pipeline.Models
{
    /// <summary>
    /// Une planche en cours de traitement : son image, son sens de lecture, et les
    /// zones de texte qu'on y a trouvées.
    /// </summary>
    /// <remarks>
    /// C'est l'objet que chaque étape du pipeline reçoit et rend. Une étape ne
    /// modifie jamais celle qu'on lui donne : elle en construit une nouvelle. C'est
    /// indispensable pour la boucle de correction — l'image d'origine et l'image
    /// nettoyée doivent coexister, la première pour tout relancer, la seconde pour
    /// réécrire une traduction corrigée sans refaire le reste.
    /// <para>
    /// Le sens de lecture vit ici, sur la donnée, et non dans le réglage d'un
    /// algorithme : c'est une propriété de la planche, pas de la façon de la traiter.
    /// Une série retournée et une série d'origine se traitent avec le même
    /// ordonnanceur.
    /// </para>
    /// <para>
    /// Les dimensions de l'image ne sont volontairement pas stockées : elles se
    /// déduisent des octets au moment où on en a besoin, et les garder ici créerait
    /// une seconde source de vérité qui peut diverger de l'image.
    /// </para>
    /// </remarks>
    public class Planche
    {
        #region Attributs

        private byte[] image;
        private SensDeLecture sens;
        private IReadOnlyList<ZoneDeTexte> zones;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise une planche à partir de son image, dans le sens du manga
        /// d'origine et sans aucune zone connue.
        /// </summary>
        /// <param name="image">
        /// Contenu binaire du fichier image, tel quel et non décodé.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="image"/> vaut <c>null</c>.
        /// </exception>
        public Planche(byte[] image)
            : this(image, SensDeLecture.DroiteAGauche, Array.Empty<ZoneDeTexte>())
        {
        }

        /// <summary>
        /// Initialise une planche à partir de son image et de son sens de lecture,
        /// sans aucune zone connue.
        /// </summary>
        /// <param name="image">
        /// Contenu binaire du fichier image, tel quel et non décodé.
        /// </param>
        /// <param name="sens">Le sens dans lequel cette planche se lit.</param>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="image"/> vaut <c>null</c>.
        /// </exception>
        public Planche(byte[] image, SensDeLecture sens)
            : this(image, sens, Array.Empty<ZoneDeTexte>())
        {
        }

        /// <summary>
        /// Initialise une planche complète.
        /// </summary>
        /// <param name="image">
        /// Contenu binaire du fichier image, tel quel et non décodé.
        /// </param>
        /// <param name="sens">Le sens dans lequel cette planche se lit.</param>
        /// <param name="zones">Les zones de texte connues à ce stade.</param>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="image"/> ou <paramref name="zones"/> vaut <c>null</c>.
        /// </exception>
        public Planche(byte[] image, SensDeLecture sens, IEnumerable<ZoneDeTexte> zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            this.image = image ?? throw new ArgumentNullException(nameof(image));
            this.sens = sens;
            this.zones = new List<ZoneDeTexte>(zones);
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Contenu binaire de l'image, tel quel et non décodé. Le format se reconnaît
        /// aux premiers octets, il n'a pas à être précisé.
        /// </summary>
        public byte[] Image
        {
            get { return image; }
        }

        /// <summary>
        /// Le sens dans lequel cette planche se lit. Beaucoup d'éditions anglaises
        /// sont retournées et se lisent de gauche à droite.
        /// </summary>
        public SensDeLecture Sens
        {
            get { return sens; }
        }

        /// <summary>
        /// Les zones de texte trouvées sur la planche. Vide tant que la lecture n'a
        /// pas eu lieu.
        /// </summary>
        public IReadOnlyList<ZoneDeTexte> Zones
        {
            get { return zones; }
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Construit une nouvelle planche portant les zones indiquées, avec la même
        /// image et le même sens de lecture.
        /// </summary>
        /// <param name="zones">Les zones de la nouvelle planche.</param>
        /// <returns>Une nouvelle planche ; celle-ci reste inchangée.</returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="zones"/> vaut <c>null</c>.
        /// </exception>
        public Planche AvecZones(IEnumerable<ZoneDeTexte> zones)
        {
            return new Planche(image, sens, zones);
        }

        /// <summary>
        /// Construit une nouvelle planche portant l'image indiquée, avec les mêmes
        /// zones et le même sens de lecture. C'est ainsi qu'une étape rend son image
        /// nettoyée ou rendue sans effacer l'originale.
        /// </summary>
        /// <param name="image">L'image de la nouvelle planche.</param>
        /// <returns>Une nouvelle planche ; celle-ci reste inchangée.</returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="image"/> vaut <c>null</c>.
        /// </exception>
        public Planche AvecImage(byte[] image)
        {
            return new Planche(image, sens, zones);
        }

        #endregion
    }
}
