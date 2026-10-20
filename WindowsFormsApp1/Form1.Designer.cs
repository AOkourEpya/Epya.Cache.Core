namespace WindowsFormsApp1
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.lblSucessWrites = new System.Windows.Forms.Label();
            this.lblFailReads = new System.Windows.Forms.Label();
            this.lblSuccessReads = new System.Windows.Forms.Label();
            this.lblCurrent = new System.Windows.Forms.Label();
            this.lblTotal = new System.Windows.Forms.Label();
            this.lblFailWrites = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.lblCacheSize = new System.Windows.Forms.Label();
            this.lbl15 = new System.Windows.Forms.Label();
            this.lblCachDep = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.listView1 = new System.Windows.Forms.ListView();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.listView2 = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(796, 450);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(225, 53);
            this.button1.TabIndex = 0;
            this.button1.Text = "Start";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(810, 322);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(37, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Status";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(920, 322);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Status";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(565, 450);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(225, 53);
            this.button2.TabIndex = 4;
            this.button2.Text = "Stop";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(810, 60);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(65, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Total Clients";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(810, 82);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(75, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Current Clients";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(810, 107);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(82, 13);
            this.label5.TabIndex = 7;
            this.label5.Text = "Success Reads";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(810, 130);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(57, 13);
            this.label6.TabIndex = 8;
            this.label6.Text = "Fail Reads";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(810, 153);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(75, 13);
            this.label7.TabIndex = 9;
            this.label7.Text = "Sucess Writes";
            // 
            // lblSucessWrites
            // 
            this.lblSucessWrites.AutoSize = true;
            this.lblSucessWrites.Location = new System.Drawing.Point(920, 153);
            this.lblSucessWrites.Name = "lblSucessWrites";
            this.lblSucessWrites.Size = new System.Drawing.Size(82, 13);
            this.lblSucessWrites.TabIndex = 14;
            this.lblSucessWrites.Text = "lblSucessWrites";
            // 
            // lblFailReads
            // 
            this.lblFailReads.AutoSize = true;
            this.lblFailReads.Location = new System.Drawing.Point(920, 130);
            this.lblFailReads.Name = "lblFailReads";
            this.lblFailReads.Size = new System.Drawing.Size(64, 13);
            this.lblFailReads.TabIndex = 13;
            this.lblFailReads.Text = "lblFailReads";
            // 
            // lblSuccessReads
            // 
            this.lblSuccessReads.AutoSize = true;
            this.lblSuccessReads.Location = new System.Drawing.Point(920, 107);
            this.lblSuccessReads.Name = "lblSuccessReads";
            this.lblSuccessReads.Size = new System.Drawing.Size(89, 13);
            this.lblSuccessReads.TabIndex = 12;
            this.lblSuccessReads.Text = "lblSuccessReads";
            // 
            // lblCurrent
            // 
            this.lblCurrent.AutoSize = true;
            this.lblCurrent.Location = new System.Drawing.Point(920, 82);
            this.lblCurrent.Name = "lblCurrent";
            this.lblCurrent.Size = new System.Drawing.Size(51, 13);
            this.lblCurrent.TabIndex = 11;
            this.lblCurrent.Text = "lblCurrent";
            // 
            // lblTotal
            // 
            this.lblTotal.AutoSize = true;
            this.lblTotal.Location = new System.Drawing.Point(920, 60);
            this.lblTotal.Name = "lblTotal";
            this.lblTotal.Size = new System.Drawing.Size(41, 13);
            this.lblTotal.TabIndex = 10;
            this.lblTotal.Text = "lblTotal";
            // 
            // lblFailWrites
            // 
            this.lblFailWrites.AutoSize = true;
            this.lblFailWrites.Location = new System.Drawing.Point(920, 178);
            this.lblFailWrites.Name = "lblFailWrites";
            this.lblFailWrites.Size = new System.Drawing.Size(63, 13);
            this.lblFailWrites.TabIndex = 16;
            this.lblFailWrites.Text = "lblFailWrites";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(810, 178);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(56, 13);
            this.label14.TabIndex = 15;
            this.label14.Text = "Fail Writes";
            // 
            // lblCacheSize
            // 
            this.lblCacheSize.AutoSize = true;
            this.lblCacheSize.Location = new System.Drawing.Point(920, 296);
            this.lblCacheSize.Name = "lblCacheSize";
            this.lblCacheSize.Size = new System.Drawing.Size(68, 13);
            this.lblCacheSize.TabIndex = 18;
            this.lblCacheSize.Text = "lblCacheSize";
            // 
            // lbl15
            // 
            this.lbl15.AutoSize = true;
            this.lbl15.Location = new System.Drawing.Point(810, 296);
            this.lbl15.Name = "lbl15";
            this.lbl15.Size = new System.Drawing.Size(61, 13);
            this.lbl15.TabIndex = 17;
            this.lbl15.Text = "Cache Size";
            // 
            // lblCachDep
            // 
            this.lblCachDep.AutoSize = true;
            this.lblCachDep.Location = new System.Drawing.Point(920, 273);
            this.lblCachDep.Name = "lblCachDep";
            this.lblCachDep.Size = new System.Drawing.Size(62, 13);
            this.lblCachDep.TabIndex = 20;
            this.lblCachDep.Text = "lblCachDep";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(810, 273);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(84, 13);
            this.label9.TabIndex = 19;
            this.label9.Text = "Cache Dep Size";
            // 
            // listView1
            // 
            this.listView1.HideSelection = false;
            this.listView1.Location = new System.Drawing.Point(11, 57);
            this.listView1.MinimumSize = new System.Drawing.Size(950, 50);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(760, 210);
            this.listView1.TabIndex = 21;
            this.listView1.UseCompatibleStateImageBehavior = false;
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(909, 383);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 20);
            this.textBox1.TabIndex = 22;
            this.textBox1.Text = "1";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(810, 386);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(66, 13);
            this.label8.TabIndex = 23;
            this.label8.Text = "Replica Size";
            //
            // listView2
            //
            this.listView2.HideSelection = false;
            this.listView2.Location = new System.Drawing.Point(11, 280);
            this.listView2.Name = "listView2";
            this.listView2.Size = new System.Drawing.Size(540, 223);
            this.listView2.TabIndex = 24;
            this.listView2.UseCompatibleStateImageBehavior = false;
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1033, 515);
            this.Controls.Add(this.listView2);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.listView1);
            this.Controls.Add(this.lblCachDep);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.lblCacheSize);
            this.Controls.Add(this.lbl15);
            this.Controls.Add(this.lblFailWrites);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.lblSucessWrites);
            this.Controls.Add(this.lblFailReads);
            this.Controls.Add(this.lblSuccessReads);
            this.Controls.Add(this.lblCurrent);
            this.Controls.Add(this.lblTotal);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label lblSucessWrites;
        private System.Windows.Forms.Label lblFailReads;
        private System.Windows.Forms.Label lblSuccessReads;
        private System.Windows.Forms.Label lblCurrent;
        private System.Windows.Forms.Label lblTotal;
        private System.Windows.Forms.Label lblFailWrites;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label lblCacheSize;
        private System.Windows.Forms.Label lbl15;
        private System.Windows.Forms.Label lblCachDep;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ListView listView2;
    }
}

