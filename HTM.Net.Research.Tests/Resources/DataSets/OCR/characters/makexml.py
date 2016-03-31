'''
I am writing this script to auto generate an xml file which I will use to
generate JPEG images of each character in a JPEG image of a pdf document 
that contains alphanumeric characters in 145 different fonts.
'''

# open xml file for writing and write header
xml_file = open('characters.xml','w')
xml_file.write('<?xml version="1.0" encoding="UTF-8"?>\n')
xml_file.write('<imagelist>\n')

chars = [ '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'uppercase_A',
  'uppercase_B', 'uppercase_C', 'uppercase_D', 'uppercase_E', 'uppercase_F',
  'uppercase_G', 'uppercase_H', 'uppercase_I', 'uppercase_J', 'uppercase_K',
  'uppercase_L', 'uppercase_M', 'uppercase_N', 'uppercase_O', 'uppercase_P',
  'uppercase_Q', 'uppercase_R', 'uppercase_S', 'uppercase_T', 'uppercase_U',
  'uppercase_V', 'uppercase_W', 'uppercase_X', 'uppercase_Y', 'uppercase_Z',
  'lowercase_a', 'lowercase_b', 'lowercase_c', 'lowercase_d', 'lowercase_e',
  'lowercase_f', 'lowercase_g', 'lowercase_h', 'lowercase_i', 'lowercase_j',
  'lowercase_k', 'lowercase_l', 'lowercase_m', 'lowercase_n', 'lowercase_o',
  'lowercase_p', 'lowercase_q', 'lowercase_r', 'lowercase_s', 'lowercase_t',
  'lowercase_u', 'lowercase_v', 'lowercase_w', 'lowercase_x', 'lowercase_y',
  'lowercase_z' ]

# read fonts.txt
fonts_file = open('fonts.txt','r')
for line in fonts_file:
  font = line.split('.')[0].replace(" ", "").replace("-", "")
  for char in chars:
    xml_file.write('  <image file="' + font + '/' + char + '.jpg"' + 
      ' tag="' + char[-1] + '" />\n')
  
fonts_file.close()

xml_file.write('</imagelist>')

xml_file.close()

