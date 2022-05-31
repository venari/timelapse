# base-image for python on any machine using a template variable,
# see more about dockerfile templates here: https://www.balena.io/docs/learn/develop/dockerfile/
FROM balenalib/aarch64-python:3-stretch-run

# RUN install_packages git

# Set our working directory
WORKDIR /usr/src/app

COPY . ./

# main.py will run when container starts up on the device
CMD ["python","-u","src/main.py"]