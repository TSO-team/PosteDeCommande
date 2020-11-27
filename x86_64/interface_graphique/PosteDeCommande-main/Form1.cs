#region les includeslstMessages
using System;
using System.Drawing;        //Pour mettre des animations
using System.IO.Ports;       // Pour utiliser le port série
using System.Windows.Forms;
#endregion

namespace ICDIBasic
{
    public partial class Form1 : Form
    {

        #region CONSTANTES
        const byte VEHICULE = 8;
        const byte ARRET = 1;
        const byte DEMARRE = 0;


        const byte ETAT_VEHICULE = 1;
        const byte ARRETE = 0;
        const byte EN_MARCHE = 1;
        const byte HORS_CIRCUIT = 2;


        const byte VITESSE = 2;

        const byte BATTERIE = 3;

        const byte COULEUR = 4;
        const byte METAL = 0;
        const byte ORANGE = 1;
        const byte NOIR = 2;

        const byte POID = 00;

        const byte NO_STATION = 7;
        const byte STATION_1 = 0;
        const byte STATION_2 = 1;
        const byte STATION_3 = 2;

        const byte HISTO = 192;

        const byte SENS = 8;
        const byte HORAIRE = 0;
        const byte ANTIHORAIRE = 1;

        const byte HORLOGE = 6;

        const byte FESTO_START = 9;
        const byte FESTO = 0;
        #endregion

        public static partial class _mode
        {
            public static string Mask1(string str, char mask = '*')
            {
                return str.Substring(0, 3).PadRight(str.Length, mask);
            }
        }

        public static partial class _color
        {
            public static string Mask2(string str, char mask = '*')
            {
                return (str.Substring(3, 2).PadLeft(5, mask)).PadRight(str.Length, mask);
            }
        }

        public static partial class _position
        {
            public static string Mask3(string str, char mask = '*')
            {
                return (str.Substring(5, 2).PadLeft(7, mask)).PadRight(str.Length, mask);
            }
        }

        public static partial class _unit
        {
            public static string Mask4(string str, char mask = '*')
            {
                return (str.Substring(7, 1).PadLeft(8, mask)).PadRight(str.Length, mask);
            }
        }

        public static partial class _weight
        {
            public static string Mask5(string str, char mask = '*')
            {
                return str.Substring(str.Length - 8).PadLeft(str.Length, mask);
            }
        }


        //private void CANReadThreadFunc()
        //{
        //    UInt32 iBuffer;
        //    TPCANStatus stsResult;

        //    iBuffer = Convert.ToUInt32(m_ReceiveEvent.SafeWaitHandle.DangerousGetHandle().ToInt32());
        //    // Sets the handle of the Receive-Event.
        //    //
        //    stsResult = PCANBasic.SetValue(m_PcanHandle, TPCANParameter.PCAN_RECEIVE_EVENT, ref iBuffer, sizeof(UInt32));

        //    if (stsResult != TPCANStatus.PCAN_ERROR_OK)
        //    {
        //        MessageBox.Show(GetFormatedError(stsResult), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        return;
        //    }

        // While this mode is selected
        /// <summary>
        /// Function for reading PCAN-Basic messages
        /// </summary>


        #region Initialisation du port série et de l'histoique
        string[] PortsDisponible = SerialPort.GetPortNames(); //Met la liste des ports série dans un tableau de string

        public Form1()
        {
            InitializeComponent();
            //InitializeBasicComponents();
            //Poltergeist.Text = "";
            // Poltergeist.Text = "0";
            timer3.Start(); // Pour gérer la clock interne du PC, celle pour la synchronisation de l'heure
            COMselector.Items.AddRange(PortsDisponible); // Affiche les ports disponibles UART1
            Connexion.Text = "Connexion";                // Le bouton sert à se connecter UART1
            Connexion.Enabled = false;                   // UART1

            if (COMselector.Items.Count > 0)
            {
                COMselector.SelectedIndex = 0;  // Le port par défaut est le premier port indexé
                BAUDselector.SelectedIndex = 4; // La vitesse par défaut est 9600
                Connexion.Enabled = true; // UART1
            }

            serialPort1.ReadTimeout = 500; // Délais maximum pour les try catch
            serialPort1.WriteTimeout = 500; //Delai maximum pour les try catch
        }
        #endregion

    


        #region Fermeture du programme lorsque l'utilisateur clique sur X
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen == true)
            {
                serialPort1.Close();
            }
        }
        #endregion

        #region Menu Fichier et compagnie
        #region Radémarrer l'application
        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }
        #endregion

        #region Quitter l'applicaition
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        #endregion

        #region Menu à propos
        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result1 = MessageBox.Show("Projet de 5ème session Par Alex, Claude, Samuel, John, Philippe", "Hey listen!", MessageBoxButtons.OK);
            if (result1 == DialogResult.Yes)
            {

            }
        }
        #endregion
        #endregion

        #region Actualisation des ports COM disponibles
        private void COMselector_Click(object sender, EventArgs e) // Lorsque l'user clique sur la liste des ports COM
        {

            COMselector.Items.Clear();                             // Flush la liste
            COMselector.Items.AddRange(SerialPort.GetPortNames()); // Relit les ports disponibles
            COMselector.SelectedIndex = 0;                         // Port par défaut = premier port
        }
        #endregion

        #region Connexion et déconnexion au RS232
        private void Connexion_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen == true)           // Si le port série est ouvert
            {
                if (Connexion.Text == "Déconnexion") // Si le programme est déjà connecté au port série
                {


                    Connexion.Text = "Connexion";
                    serialPort1.Close();           // Essaie de te décoonnecter


                }
            }
            else // Lorsque le programme n'est pas connecté au port série et qu'un port série est disponible
            {

                serialPort1.BaudRate = Convert.ToInt32(BAUDselector.Text); //Le baud rate choisi dans la combobox
                serialPort1.Parity = Parity.None;      //Aucune parité
                serialPort1.StopBits = StopBits.One;     //1 stop bit
                serialPort1.DataBits = 8;                //8 data bit
                serialPort1.Handshake = Handshake.None;   //pas de handshake
                serialPort1.PortName = COMselector.Text; //Avec le port série choisi
                serialPort1.Open();                       //Maintenant, connecte-toi 
                Connexion.Text = "Déconnexion";

                timer1.Start();                           // Début du Timer du heartBeat
            }
        }
        #endregion

        #region Timer de 1 seconde



        #endregion

        #region Timer de battement du HeartBeat et de gestion de la clock
        private void timer2_Tick(object sender, EventArgs e) // Timer de remise à zero de l'affichage
        {                                                    // du heartbeat et des leds rxtx
            HeartBeatOUT.BackColor = Color.White;
            HeartBeatIN.BackColor = Color.White;
            RXLED.BackColor = Color.White;
            TXLED.BackColor = Color.White;
        }
        #endregion




        //#region Bouton Démarrer le véhicule
        //private void button3_Click(object sender, EventArgs e)
        //{

        //     TxCan2(VEHICULE, DEMARRE);
        //     TxCan2(99, 99);
        //}
        //#endregion

        #region Bouton Arrêter le véhicule

        //private void button2_Click(object sender, EventArgs e)
        //{

        //     TxCan2(VEHICULE, ARRET);
        //     TxCan2(99, 99);
        //}
        //#endregion        



        //#region Lecture du CAN aux 50 ms et prise de décisions
        //private void timer4RealTimeCAN_Tick(object sender, EventArgs e)  // À toutes les 50 ms
        //{
        //    ListViewItem lviCurrentItem;
        //    foreach (MessageStatus msgStatus in m_LastMsgsList)
        //        {
        //            if (msgStatus.MarkedAsUpdated)
        //            { // ATTENTION: CETTE FONCTIONNALITÉ UTILISE UN GHOST LABEL
        //                msgStatus.MarkedAsUpdated = false;
        //                lviCurrentItem = lstMessages.Items[msgStatus.Position];
        //                lviCurrentItem.SubItems[2].Text = msgStatus.CANMsg.LEN.ToString();
        //                lviCurrentItem.SubItems[3].Text = msgStatus.DataString;
        //                lviCurrentItem.SubItems[4].Text = msgStatus.Count.ToString();
        //                lviCurrentItem.SubItems[5].Text = msgStatus.TimeString;

        //                Poltergeist.Text = lviCurrentItem.SubItems[3].Text; // Met le string lu dans le GHOST LABEL master
        //                PoidsLabel.Text = lviCurrentItem.SubItems[3].Text;
        //                GhostLabelDeRéception.Text = Poltergeist.Text; // Met le string lu dans un GHOST LABEL
        //                RangedTrame.Text = Poltergeist.Text;           // Met le string lu dans un GHOST LABEL
        //                RangedTrame.Text = RangedTrame.Text.Remove(RangedTrame.Text.Length - 7); // Enlève la timestamp et la variation
        //                GhostLabelDeRéception.Text = GhostLabelDeRéception.Text.Remove(GhostLabelDeRéception.Text.Length - 4); // Enlève la valeur variable et la timestamp

        //                string input = Poltergeist.Text;
        //                string sub = input.Substring(3, 2);
        //                Range.Text = sub;
        //            }
        //        }


        //    if(CANid.Text == "ListViewSubItem: {005h}")
        //    {
        //        string Dizaine = Poltergeist.Text.Remove(0, 1);
        //        Dizaine = Dizaine.Remove(Dizaine.Length - 12);

        //        string Unite = Poltergeist.Text.Remove(0, 4);
        //        Unite = Unite.Remove(Unite.Length - 9);

        //        string Dizieme = Poltergeist.Text.Remove(0, 10);
        //        Dizieme = Dizieme.Remove(Dizieme.Length - 3);

        //        string Centieme = Poltergeist.Text.Remove(0, 13);

        //       // int Dix = Convert.ToInt16(Dizaine);
        //       // int Un = Convert.ToInt16(Unite);
        //       // int Diz = Convert.ToInt16(Dizieme);
        //       // int Cnt = Convert.ToInt16(Centieme);

        //        //string Newton = Convert.ToString(Dix + Un + (Diz / 10) + (Cnt / 100));


        //            LblPoidBloc.Text = Dizaine;
        //            Poidunite.Text = Unite;
        //            PoidDizieme.Text = Dizieme;
        //            PoidCentieme.Text = Centieme;                      
        //            pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;



        //            TxCan2(VEHICULE, DEMARRE);
        //            TxCan2(99, 99);          
        //    }
        //}
        //#endregion

        #region Directives de changement de direction pour le véhicule

        private void lblDirection_TextChanged(object sender, EventArgs e)
        {
            //TPCANMsg CANMsg;
            //TPCANStatus stsResult;
            //CANMsg = new TPCANMsg();
            //CANMsg.DATA = new byte[2];
            //CANMsg.ID = 004;
            //CANMsg.LEN = 4;
            //CANMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;

            //for (int i = 0; i < CANMsg.LEN; i++)
            //  {
            //   if (i == 0) { CANMsg.DATA[0] = 08; }
            //   if (i == 1) { CANMsg.DATA[1] = 00; }
            //   if (i == 2) { CANMsg.DATA[2] = 00; }
            //   if (i == 3) { CANMsg.DATA[3] = 00; }
            //   if (i == 4) { CANMsg.DATA[4] = 00; }
            //   if (i == 5) { CANMsg.DATA[5] = 00; }
            //   if (i == 6) { CANMsg.DATA[6] = 00; }
            //   if (i == 7) { CANMsg.DATA[7] = 00; }
            //  }
            //   stsResult = PCANBasic.Write(m_PcanHandle, ref CANMsg);       


            //}


        }
        #endregion





        private void RangedTrame_TextChanged(object sender, EventArgs e)
        {
            if (RangedTrame.Text == "02") // Indice de vitesse
            {
                lblSpeed.Text = (Int32.Parse(Range.Text, System.Globalization.NumberStyles.HexNumber)).ToString();
            }

            if (RangedTrame.Text == "03") // Indice de batterie
            {
                lblBattryLevel.Text = (Int32.Parse(Range.Text, System.Globalization.NumberStyles.HexNumber)).ToString();
            }
        }

        private void GhostLabelDeRéception_TextChanged(object sender, EventArgs e)
        {
            if (GhostLabelDeRéception.Text == "01 00") // Lorsque le véhicule est arrêté
            {
                lblEtatVehicule.Text = "Arrêté";
            }

            if (GhostLabelDeRéception.Text == "01 01") // Lorsque le véhicule est en marche
            {
                lblEtatVehicule.Text = "En marche";
            }

            if (GhostLabelDeRéception.Text == "01 02") // Lorsque le véhicule est hors circuit
            {
                lblEtatVehicule.Text = "En marche";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.hm;
            }

            if (Poltergeist.Text.Contains("34 30")) // Lorsque le bloc est métallique
            {
                lblBlocColor.Text = "Métalique";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.metal;
                label32.Text = "Metal";
            }

            if (Poltergeist.Text.Contains("34 32")) // Lorsque le bloc est noir
            {
                lblBlocColor.Text = "Noir";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.noir;
                label32.Text = "Noir";
            }

            if (Poltergeist.Text.Contains("34 31")) // Lorsque le bloc est orange
            {
                lblBlocColor.Text = "Orange";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.orange;
                label32.Text = "Orange";
            }

            if (Poltergeist.Text.Contains("37 30")) // Lorsque le véhicule est à la station de pesée
            {
                lblStation.Text = "Station de pesée";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;

            }

            if (Poltergeist.Text.Contains("37 31")) //Le véhicule est à la table FESTO
            {
                lblStation.Text = "Table FESTO";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.festoHoraire;



            }

            if (GhostLabelDeRéception.Text == "07 02") // Le véhicule est à la station 3
            {
                lblStation.Text = "Station 3";
            }


        }

        //private void button3_Click_1(object sender, EventArgs e)
        //{

        //        TPCANMsg CANMsg;
        //        TPCANStatus stsResult;

        //        CANMsg = new TPCANMsg();
        //        CANMsg.DATA = new byte[8];

        //        CANMsg.ID = 003; // 006 c'est pour faire des tests avec le fichier .HEX . Mettre 004 pour la version finale
        //        CANMsg.LEN = 7;
        //        CANMsg.MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD;

        //        for (int i = 0; i < CANMsg.LEN; i++) // Incrémenteur de longueur de message
        //        {
        //            if (i == 0) { CANMsg.DATA[0] = 43; } // Note: Ce programme en C# envoie en base 10
        //            if (i == 1) { CANMsg.DATA[1] = 30; }   // 0x30 (en ascii) = caractère '0'
        //            if (i == 2) { CANMsg.DATA[2] = Convert.ToByte(DateTime.Now.ToString("HH")); } // 0 (en base 10) = caractère ascii '0' (0x30)
        //            if (i == 3) { CANMsg.DATA[3] = Convert.ToByte(DateTime.Now.ToString("mm")); }
        //            if (i == 4) { CANMsg.DATA[4] = Convert.ToByte(DateTime.Now.ToString("ss")); }
        //        }
        //        stsResult = PCANBasic.Write(m_PcanHandle, ref CANMsg); // Écrit le message

        //        if (stsResult == TPCANStatus.PCAN_ERROR_OK) // Si l'envoie est réussi
        //        {

        //        }

        //        else // Si l'envoi à échoué
        //        {

        //        }


        //}

        private void button4_Click(object sender, EventArgs e)
        {

            //TxCan2(FESTO_START, FESTO);
            //TxCan2(99, 99);
        }

        private void quitterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void redémarrerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }

        private void àProposToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result1 = MessageBox.Show("Projet de 5ème session Par Claude, Alex, Samuel, John, Philippe", "Hey listen!", MessageBoxButtons.OK); // Note: À moifier
            if (result1 == DialogResult.Yes)
            {

            }
        }

        private void Poltergeist_TextChanged(object sender, EventArgs e)
        {
            if (GhostLabelDeRéception.Text == "31 30") // Lorsque le véhicule est arrêté
            {
                lblEtatVehicule.Text = "Arrêté";
            }

            if (GhostLabelDeRéception.Text == "31 31") // Lorsque le véhicule est en marche
            {
                lblEtatVehicule.Text = "En marche";
            }

            if (GhostLabelDeRéception.Text == "01 02") // Lorsque le véhicule est hors circuit
            {
                lblEtatVehicule.Text = "En marche";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.hm;
            }

            if (Poltergeist.Text == "34 30") // Lorsque le bloc est métallique
            {
                lblBlocColor.Text = "Métalique";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.metal;
                label32.Text = "Metal";
            }

            if (Poltergeist.Text == "34 32") // Lorsque le bloc est noir
            {
                lblBlocColor.Text = "Noir";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.noir;
                label32.Text = "Noir";
            }

            if (Poltergeist.Text == "34 31") // Lorsque le bloc est orange
            {
                lblBlocColor.Text = "Orange";
                pictureBox3.Image = PCANBasicExample.Properties.Resources.orange;
                label32.Text = "Orange";
            }

            if (GhostLabelDeRéception.Text == "07 00") // Lorsque le véhicule est à la station de pesée
            {
                lblStation.Text = "Station de pesée";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.poidsHoraire;

            }

            if (GhostLabelDeRéception.Text == "07 01") //Le véhicule est à la table FESTO
            {
                lblStation.Text = "Table FESTO";
                pictureBox1.Image = PCANBasicExample.Properties.Resources.festoHoraire;

            }

            if (GhostLabelDeRéception.Text == "07 02") // Le véhicule est à la station 3
            {
                lblStation.Text = "Station 3";
            }
        }

        private void lblBattryLevel_Click(object sender, EventArgs e)
        {

        }

        private void Poidunite_Click(object sender, EventArgs e)
        {

        }

        private void PoidDizieme_Click(object sender, EventArgs e)
        {

        }

        private void PoidCentieme_Click(object sender, EventArgs e)
        {

        }

        private void label33_Click(object sender, EventArgs e)
        {

        }

        private void LblPoidBloc_Click(object sender, EventArgs e)
        {
            if (_weight == 0)
            {

            }
            else
            {
                Convert.ToInt32(Mask5, 2).ToString();                                              // mettre le bon nom de la trame can recu!!
                LblPoidBloc == NomDeLaVariable
            }

        }
    }
}
#endregion
