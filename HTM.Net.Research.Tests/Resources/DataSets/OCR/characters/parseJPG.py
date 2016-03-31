#!/usr/bin/python2

'''
This script parses JPEG images of text documents to isolate and save images
of individual characters.  The size of these output images in pixels is 
specified by the parameters desired_height and desired_width.

The JPEG images are converted to grey scale using a parameter called 
luminance_threshold to distinguish between light and dark pixels.  Lines of 
text are found by searching for rows that contain dark pixels, and
characters are found by searching for columns that contain dark pixels. Once
a character is found it is padded with blank rows and columns to obtain the
desired size.  The images are saved using the filenames given in the XML file.
'''

# Set desired output image height and width in pixels
desired_height = 32
desired_width = 32

DEBUG = False

import matplotlib.pyplot as plot
import numpy as np
import operator
import sys
import re
import os

from PIL import Image
from xml.dom import minidom


jpg_list = [ 'characters-0.jpg', 'characters-1.jpg', 'characters-2.jpg',
  'characters-3.jpg', 'characters-4.jpg', 'characters-5.jpg',
  'characters-6.jpg', 'characters-7.jpg', 'characters-8.jpg',
  'characters-9.jpg', 'characters-10.jpg', 'characters-11.jpg',
  'characters-12.jpg', 'characters-13.jpg', 'characters-14.jpg',
  'characters-15.jpg', 'characters-16.jpg', 'characters-17.jpg',
  'characters-18.jpg', 'characters-19.jpg' ]

#jpg_list = [ 'debug_doc.jpg' ]

# Parse XML file for filenames to use when saving each character image
xmldoc = minidom.parse('characters.xml')
#xmldoc = minidom.parse('debug_doc.xml')
filelist = xmldoc.getElementsByTagName('image') 
print len(filelist)
#for i in range(145):
  #print filelist[62*i].attributes['file'].value

# this counter gets used to select file names from an xml file
output_files_saved = 0
  
for jpg in jpg_list:
  print jpg
  im = Image.open(jpg)
  width, length = im.size
  if DEBUG:
    print "image size: ", im.size
    print "image mode: ", im.mode
    print im.size[1],im.size[0]
  
  # read pixel data from image into a numpy array
  if im.mode == 'L':
    pixels = np.array(list(im.getdata())).reshape(im.size[1],im.size[0])
  elif im.mode == 'RGB':
    pixels = np.array(list(im.convert('L').getdata())).reshape(im.size[1],
      im.size[0])
  
  #im.show()
  
  ##############################################################################
  # Removed all logic for determining the value to use to distinguish between 
  # light and dark pixels because this is a non-trivial challenge of its own and
  # I want to get to generating a data set for OCR which I can do much faster by 
  # choosing the threshold manually.
  ##############################################################################
  
  luminance_threshold = 100
    
  
  ##############################################################################
  # parse document for lines of text 
  ##############################################################################
  
  row = 0
  while row < length:
    # Find the first row of pixels in next line of text by ignoring blank rows 
    # of pixels which will have a non-zero product since white pixels have a
    # luminance value of 255
    #row_data = pixels[row * width : row * width + width]
    while (row < length and pixels[row,:].min() > luminance_threshold):
      row += 1
    first_row = row
    if DEBUG:
      print "the first row of pixels in the line of text is ", first_row
    # Find the last row of pixels in this line of text by counting rows with
    # dark pixels. These rows have a product of zero since the luminance value
    # of all dark pixels was set to zero
    while (row < length and pixels[row:row + 2,:].min() < luminance_threshold):
      row += 1
    last_row = row
    #if row < length:
      #last_row = row + 2  # this is a hack for Cochin font Q
    #row += 5  # this is a hack for Cochin font Q
    if DEBUG:
      print "the last row of pixels in the line of text is ", last_row
  
  ##############################################################################
  # parse line of text for characters
  ##############################################################################
  
    if first_row < last_row:
      col = 0
      while col < width:
        # find first column of pixels in the next character by ignoring blank 
        # cols of pixels
        while col < width and pixels[first_row:last_row,col].min() > luminance_threshold:
          col += 1
        first_col = col
        # find last column of pixels in the next character by counting columns 
        # with dark pixels
        while col < width and \
          pixels[first_row:last_row,col:col + 5].min() < luminance_threshold:
          col += 1
        last_col = col
  
  ##############################################################################
  # remove blank rows from the top and bottom of characters
  ##############################################################################
  
        if first_col < last_col:
          # remove blank rows from the top of the character
          r = first_row;
          while pixels[r,first_col:last_col].min() > luminance_threshold:
            r = r + 1;
          char_first_row = r;
          # remove blank rows from the bottom of the character
          r = last_row;
          while pixels[r,first_col:last_col].min() > luminance_threshold:
            r = r - 1;
          char_last_row = r + 1;
          if DEBUG:
            # isolate an image of this character
            character = im.crop([first_col, char_first_row, last_col, 
              char_last_row])
            print "Character size after whitespace removal", character.size
            print first_col, first_row, last_col, last_row
            #character.show()
          # pad character width out to desired_width
          char_width = last_col - first_col
          if char_width > desired_width:
            print "Character is wider than ", desired_width
          else:
            # add the same number of blank columns to the left and right
            first_col = first_col - (desired_width - char_width) / 2
            last_col = last_col + (desired_width - char_width) / 2
            # if the difference was odd we'll be short one column
            char_width = last_col - first_col
            if char_width < desired_width:
              last_col = last_col + 1
          # pad character height out to desired_height
          char_height = char_last_row - char_first_row
          if char_height > desired_height:
            print "Character is taller than ", desired_height
          else:
            # add the same number of blank rows to the left and right
            char_first_row = char_first_row - (desired_height - char_height) / 2
            char_last_row = char_last_row + (desired_height - char_height) / 2
            # if the difference was odd we'll be short one row
            char_height = char_last_row - char_first_row
            if char_height < desired_height:
              char_last_row = char_last_row + 1
          character = im.crop([first_col, char_first_row, last_col, 
            char_last_row])
          if DEBUG:
            print "Character size after padding", character.size
            print first_col, char_first_row, last_col, char_last_row
            #character.show()
            #garbage = raw_input()
          # save image to filename specified in ground truth file
          filename = filelist[output_files_saved].attributes['file'].value
          directory = filename.split('/')[0]
          if not os.path.exists(directory):
            os.makedirs(directory)
          character.save(filename, "JPEG", quality=80)
          output_files_saved = output_files_saved + 1
    
print output_files_saved  
  
