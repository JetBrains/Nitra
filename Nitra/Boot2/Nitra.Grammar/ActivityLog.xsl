<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" >
    <xsl:output method="html"  encoding="utf-16"/>
    <xsl:template match="activity">
        <head>
        <title>Activity Monitor Log</title>
        <style type="text/css">
            body{ text-align: left; width: 100%;  font-family: Verdana, sans-serif; }

            table{ border: none;  border-collapse: separate;  width: 100%; }

            tr.title td{ font-size: 24px;  font-weight: bold; }

            th{ background: #d0d0d0;  font-weight: bold;  font-size: 10pt;  text-align: left; }
            tr{ background: #eeeeee}
            td, th{ font-size: 8pt;  padding: 1px;  border: none; }

            tr.info td{}
            tr.warning td{background-color:yellow;color:black}
            tr.error td{background-color:red;color:black}
            
            span {text-decoration:underline}
            a:hover{text-transform:uppercase;color: #9090F0;}
        </style>
        </head>

        <body>      
        <table>
            <tr class="title">
                <td colspan="7">Activity Monitor Log</td>
            </tr>             
            <tr>
                <td colspan="2">infos</td>
                <td colspan="5"><xsl:value-of select="count(entry[type='Information'])"/></td>
            </tr>
            <tr>
                <td colspan="2">warnings</td>
                <td colspan="5"><xsl:value-of select="count(entry[type='Warning'])"/></td>
            </tr>
            <tr>
                <td colspan="2">errors</td>
                <td colspan="5"><xsl:value-of select="count(entry[type='Error'])"/></td>
            </tr>
            <tr>
                <th width="20">#</th>
                <th width="50">Type</th>
                <th>Description</th>
                <th width="280">GUID</th>
                <th>Hr</th>                
                <th>Source</th>
                <th>Time (UTC)</th>
            </tr>               
            <xsl:apply-templates/>
        </table>

        </body>
    </xsl:template>

    <xsl:template match="entry">
        <!-- example 
        
          <entry>
            <record>136</record>
            <time>2004/02/26 00:42:59.706</time>
            <type>Error</type>
            <source>Microsoft Visual Studio</source>
            <description>Loading UI library</description>
            <guid>{00000000-0000-0000-0000-000000000000}</guid>
            <hr>800a006f</hr>
            <path></path>
        </entry>
        
        -->
        <xsl:choose>

            <xsl:when test="type='Information'">
                    <tr id="info" class="info">
                        <td><xsl:value-of select="record"/></td>
                        <td></td>
                        <xsl:call-template name="row"/>
                    </tr>                
            </xsl:when>                

            <xsl:when test="type='Warning'">
                    <tr id="warning" class="warning">
                        <td><xsl:value-of select="record"/></td>
                        <td>Warning</td>
                        <xsl:call-template name="row"/>
                    </tr>                
            </xsl:when>             

            <xsl:when test="type='Error'">
                    <tr id="error" class="error">
                        <td><xsl:value-of select="record"/></td>
                        <td>ERROR</td>
                        <xsl:call-template name="row"/>
                    </tr>                
            </xsl:when>                

        </xsl:choose>  

    </xsl:template>
    
    <xsl:template name="row">
                <td id="description"><xsl:value-of select="description"/><xsl:if test="path"><br/>&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;&#160;<xsl:value-of select="path"/></xsl:if></td>
                <td id="guid"><xsl:value-of select="guid"/></td>    
                <td id="hr"><xsl:value-of select="hr"/></td>    
                <td><xsl:value-of select="source"/></td>    
                <td><xsl:value-of select="time"/></td>
    </xsl:template>            

</xsl:stylesheet>