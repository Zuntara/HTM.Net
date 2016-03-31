'''
I am writing this script to auto generate a tex file which I will use to
generate a pdf document containing alphanumeric characters in a wide variety
of fonts.
'''

# open tex file for writing and write document preamble
tex_file = open('characters.tex','w')
tex_file.write('\\documentclass[10pt,border=10pt]{article}\n')
tex_file.write('\n')
tex_file.write('\\usepackage[margin=1in]{geometry}\n')
tex_file.write('\\usepackage{setspace}\n')
tex_file.write('\\doublespacing\n')
tex_file.write('\n')
tex_file.write('\\usepackage{fontspec}\n')
tex_file.write('\n')
tex_file.write('\\begin{document}\n')
tex_file.write('\n')
tex_file.write('\\pagenumbering{gobble}\n')
tex_file.write('\n')

# read fonts.txt
fonts_file = open('fonts.txt','r')
for line in fonts_file:
  font_file = line.replace("\n", "")
  font = line.split('.')[0]
  font_slug = font.replace(" ", "").replace("-", "")
  #tex_file.write('\\newfontfamily\\' + font_slug + '{' + font_file + '}' + '\n')
  tex_file.write('\\newfontfamily\\' + font_slug + '[Path=/Library/Fonts/]{' + 
    font_file + '}' + '\n')
  tex_file.write('\\' + font_slug + '\n')
  tex_file.write('\n')
  #tex_file.write(font + '\n')
  tex_file.write('\input{alphanumchars}\n')
  tex_file.write('\n')
  #tex_file.write('% bold\n')
  #tex_file.write('\\textbf{\n')
  #tex_file.write('\input{alphanumchars}\n')
  #tex_file.write('}\n')
  #tex_file.write('\n')
  #tex_file.write('% italic\n')
  #tex_file.write('\\textit{\n')
  #tex_file.write('\input{alphanumchars}\n')
  #tex_file.write('}\n')
  #tex_file.write('\n')
  
fonts_file.close()


tex_file.write('\end{document}\n')
tex_file.close()
