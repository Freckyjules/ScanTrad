namespace ScanTrad.Pipeline.Models
{
    /// <summary>
    /// Une zone de texte trouvée sur une page : où elle se trouve, ce qu'elle dit,
    /// et ce qu'elle dira une fois traduite.
    /// </summary>
    /// <remarks>
    /// L'objet s'enrichit au fil du pipeline. Le lecteur de page pose la géométrie et le
    /// texte original ; la traduction remplit le texte traduit ; le tri renseigne
    /// l'ordre de lecture. Un champ à <c>null</c> signifie donc « pas encore
    /// calculé », et non « vide pour de bon ».
    /// </remarks>
    public class ZoneDeTexte
    {
        #region Attributs

        private Quadrilatere rectangle;
        private double angle;
        private double? hauteurDeLigne;
        private Bulle? bulle;
        private Couleur? couleurDeFond;
        private string texteOriginal;
        private string? texteTraduit;
        private double confiance;
        private int? ordreDeLecture;

        #endregion

        #region Constructeurs

        /// <summary>
        /// Initialise une zone vide, sans géométrie ni texte.
        /// </summary>
        public ZoneDeTexte()
        {
            rectangle = new Quadrilatere();
            angle = 0;
            hauteurDeLigne = null;
            bulle = null;
            couleurDeFond = null;
            texteOriginal = string.Empty;
            texteTraduit = null;
            confiance = 0;
            ordreDeLecture = null;
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// L'emprise du bloc de texte sur la planche. C'est un rectangle droit — ce
        /// que rend le détecteur — et le type le porte comme un cas particulier de
        /// quadrilatère. L'inclinaison du texte est dans <see cref="Angle"/>.
        /// </summary>
        public Quadrilatere Rectangle
        {
            get { return rectangle; }
            set { rectangle = value; }
        }

        /// <summary>
        /// L'inclinaison du texte par rapport à la planche, en degrés. Zéro pour un
        /// texte horizontal, positif s'il descend vers la droite — l'axe vertical
        /// d'une image étant orienté vers le bas.
        /// </summary>
        /// <remarks>
        /// Mesurée à la lecture, sur les boîtes des lignes que rend le moteur d'OCR.
        /// Elle ne se déduit pas de <see cref="Rectangle"/>, qui est droit par
        /// construction : c'est pour ça qu'elle est stockée.
        /// </remarks>
        public double Angle
        {
            get { return angle; }
            set { angle = value; }
        }

        /// <summary>
        /// La hauteur d'une ligne du texte d'origine, en pixels, ou <c>null</c> si
        /// elle n'a pas été mesurée. Sert de point de départ au rendu pour choisir sa
        /// taille de police.
        /// </summary>
        /// <remarks>
        /// À ne pas confondre avec la hauteur de <see cref="Rectangle"/>, qui couvre
        /// le bloc entier — trois cents pixels pour six lignes. Le nombre de lignes
        /// d'origine étant perdu quand on recolle les textes, cette hauteur ne se
        /// retrouve pas autrement.
        /// </remarks>
        public double? HauteurDeLigne
        {
            get { return hauteurDeLigne; }
            set { hauteurDeLigne = value; }
        }

        /// <summary>
        /// Le contour de la bulle contenant ce texte, ou <c>null</c> s'il n'y en a
        /// pas. C'est la zone qu'on a le droit d'effacer, et dans laquelle le texte
        /// français devra tenir. Une zone sans bulle est le plus souvent une
        /// onomatopée dessinée à même la planche.
        /// </summary>
        public Bulle? Bulle
        {
            get { return bulle; }
            set { bulle = value; }
        }

        /// <summary>
        /// La couleur du papier à l'intérieur de la bulle, ou <c>null</c> si elle n'a
        /// pas été mesurée. C'est de cette couleur que l'effacement repeint, pour que
        /// la zone nettoyée se fonde dans la bulle au lieu d'y faire une tache.
        /// </summary>
        /// <remarks>
        /// Mesurée à la lecture, sur les pixels que la diffusion a parcourus — donc
        /// sur le fond seul, l'encre des lettres ayant arrêté la diffusion. Elle est
        /// stockée parce qu'elle ne se retrouve pas ensuite : ce qui survit est un
        /// contour, et non l'ensemble des pixels que la diffusion avait retenus.
        /// <para>
        /// Une bulle blanche donne rarement 255 : les scans tirent vers le crème ou le
        /// gris, et c'est justement l'écart que cette mesure rattrape.
        /// </para>
        /// </remarks>
        public Couleur? CouleurDeFond
        {
            get { return couleurDeFond; }
            set { couleurDeFond = value; }
        }

        /// <summary>
        /// Le texte lu par le lecteur de page, dans la langue d'origine.
        /// </summary>
        public string TexteOriginal
        {
            get { return texteOriginal; }
            set { texteOriginal = value; }
        }

        /// <summary>
        /// Le texte français qui sera réécrit sur l'image, ou <c>null</c> tant que la
        /// traduction n'a pas eu lieu. C'est le champ que l'utilisateur corrige depuis
        /// le front. Une chaîne vide n'a pas le même sens que <c>null</c> : elle veut
        /// dire que la zone a été traduite par rien, et ne doit donc rien afficher.
        /// </summary>
        public string? TexteTraduit
        {
            get { return texteTraduit; }
            set { texteTraduit = value; }
        }

        /// <summary>
        /// La certitude du moteur sur ce qu'il a lu, entre 0 et 1. Sert à écarter
        /// les fausses détections : un motif de vêtement pris pour du texte ressort
        /// avec une confiance très basse.
        /// </summary>
        public double Confiance
        {
            get { return confiance; }
            set { confiance = value; }
        }

        /// <summary>
        /// Le rang de la zone dans l'ordre de lecture de la page, à partir de zéro,
        /// ou <c>null</c> tant que le tri n'a pas eu lieu. Le lecteur de page ne le
        /// renseigne pas : c'est une étape de tri séparée qui le calcule, parce
        /// qu'elle seule sait qu'un manga se lit de droite à gauche.
        /// </summary>
        public int? OrdreDeLecture
        {
            get { return ordreDeLecture; }
            set { ordreDeLecture = value; }
        }

        #endregion

        #region Méthodes

        /// <summary>
        /// Décrit la zone sur plusieurs lignes, avec tout ce qu'elle porte. Les
        /// champs pas encore calculés le disent plutôt que d'afficher du vide, pour
        /// qu'on distingue « pas encore fait » de « fait, et le résultat est vide ».
        /// </summary>
        /// <returns>Une description lisible de la zone, sur plusieurs lignes.</returns>
        public override string ToString()
        {
            string traduction = texteTraduit == null
                ? "(pas encore traduit)"
                : "« " + texteTraduit + " »";

            string rang = ordreDeLecture == null
                ? "(pas encore trié)"
                : FormattableString.Invariant($"{ordreDeLecture.Value}");

            string descriptionBulle = bulle == null
                ? "(aucune)"
                : bulle.ToString();

            string hauteur = hauteurDeLigne == null
                ? "(pas mesurée)"
                : FormattableString.Invariant($"{hauteurDeLigne.Value:0.#} px");

            string fond = couleurDeFond == null
                ? "(pas mesurée)"
                : couleurDeFond.ToString();

            string[] lignes =
            {
                FormattableString.Invariant($"texte original : « {texteOriginal} »"),
                FormattableString.Invariant($"traduction     : {traduction}"),
                FormattableString.Invariant($"confiance      : {confiance:0.###}"),
                FormattableString.Invariant($"rectangle      : {rectangle}"),
                FormattableString.Invariant($"angle du texte : {angle:0.#}°"),
                FormattableString.Invariant($"hauteur ligne  : {hauteur}"),
                FormattableString.Invariant($"couleur fond   : {fond}"),
                FormattableString.Invariant($"bulle          : {descriptionBulle}"),
                FormattableString.Invariant($"ordre lecture  : {rang}")
            };

            return string.Join(Environment.NewLine, lignes);
        }

        #endregion
    }
}
