﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace dinmore.api.Models
{

    public class Face
    {

        public FaceRectangle faceRectangle { get; set; }

        public Scores scores { get; set; }

    }
    
}
