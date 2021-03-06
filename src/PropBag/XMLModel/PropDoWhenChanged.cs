﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Serialization;

namespace DRM.PropBag.XMLModel
{
    public class PropDoWhenChanged
    {
        [XmlText]
        public string DoWhenChanged { get; set; }

        [XmlAttribute("do-after-notify")]
        public bool DoAfterNotify { get; set; }

        public PropDoWhenChanged() : this(null) {}

        public PropDoWhenChanged(string doWhenChangedDelegateName, bool doAfterNotify = false)
        {
            DoWhenChanged = doWhenChangedDelegateName;
            DoAfterNotify = doAfterNotify;
        }
    }
}
