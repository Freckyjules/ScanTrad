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

        private Quadrilatere quadrilatere;
        private Bulle? bulle;
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
            quadrilatere = new Quadrilatere();
            bulle = null;
            texteOriginal = string.Empty;
            texteTraduit = null;
            confiance = 0;
            ordreDeLecture = null;
        }

        #endregion

        #region Propriétés

        /// <summary>
        /// Les quatre coins délimitant le texte. Leur hauteur donne la taille de
        /// police d'origine, leur inclinaison donne l'angle du texte.
        /// </summary>
        public Quadrilatere Quadrilatere
        {
            get { return quadrilatere; }
            set { quadrilatere = value; }
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
    }
}
